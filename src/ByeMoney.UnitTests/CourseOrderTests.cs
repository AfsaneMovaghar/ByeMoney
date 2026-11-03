using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Modules.Purchases.Orders.Constants;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminCourseOrder;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminCourseOrders;
using ByeMoney.Application.Modules.Purchases.Queries.GetMyCourseOrder;
using ByeMoney.Application.Modules.Purchases.Queries.GetMyCourseOrders;
using System.Security.Cryptography;
using System.Text;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentAssertions;
using Moq;
using ByeMoney.API.Controllers;
using ByeMoney.Domain.Modules.Identity.Constants;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;

namespace ByeMoney.UnitTests;

public sealed class CourseOrderTests
{
    private readonly User _buyer = User.CreateInitial("tracking-buyer");

    [Fact]
    public void PreparingCartHasUnknownAmountsAndNoInventedPurchases()
    {
        var cart = CreateCart("a", "b");
        var order = Map([cart]).Single().Details;
        order.FinancialStatus.Should().Be("Preparing");
        order.FinalAmountInNoor.Should().BeNull();
        order.ItemCount.Should().Be(2);
        order.Items.Should().OnlyContain(item => item.PurchaseId == null && item.Title == null &&
            item.FinalAmountInNoor == null && item.TransactionId == null);
        order.TopUps.Should().BeEmpty();
    }

    [Fact]
    public void CartGroupsMultiplePurchasesAndKeepsSnapshotPricesAndPartialDelivery()
    {
        var cart = QuotedCart();
        cart.MarkPaid();
        var first = Purchase("a", 0, cart);
        first.MarkNotificationSent();
        var second = Purchase("b", 30, cart);
        second.RecordNotificationFailure("خطای آزمایشی تحویل");
        var rows = Map([cart], purchases: [first, second]);
        rows.Should().ContainSingle();
        var order = rows.Single().Details;
        order.FinancialStatus.Should().Be("Paid");
        order.DeliveryStatus.Should().Be("PartiallyDelivered");
        order.FinalAmountInNoor.Should().Be(30);
        order.Items[0].PriceInNoor.Should().Be(100);
        order.Items[0].FinalAmountInNoor.Should().Be(0);
        order.Items[1].FinancialStatus.Should().Be("Paid");
        order.Items[1].DeliveryStatus.Should().Be("Pending");
        order.Items[1].Title.Should().Be("عنوان خرید b");
        second.MarkNotificationSent();
        Map([cart], purchases: [first, second]).Single().Details.DeliveryStatus.Should().Be("Delivered");
    }

    [Fact]
    public void ConfirmedTopUpDoesNotMeanSuccessfulPurchaseAndReopenKeepsIdentityAndAllTopUps()
    {
        var cart = QuotedCart();
        var old = TopUp();
        old.Confirm("confirmed-topup");
        cart.AwaitTopUp(old.Id.Value);
        old.AttachToCart(cart.Id);
        Map([cart], [old]).Single().Details.FinancialStatus.Should().Be("Processing");
        cart.Fail();
        var failed = Map([cart], [old]).Single().Details;
        failed.FinancialStatus.Should().Be("Failed");
        failed.DeliveryStatus.Should().Be("NotStarted");
        cart.RecordSynchronization(true, null);
        cart.Reopen();
        Quote(cart);
        var current = TopUp();
        cart.AwaitTopUp(current.Id.Value);
        current.AttachToCart(cart.Id);
        var reopened = Map([cart], [old, current]).Single().Details;
        reopened.OrderId.Should().Be(failed.OrderId);
        reopened.TopUps.Should().HaveCount(2);
        reopened.CurrentTopUpRequestId.Should().Be(current.Id.Value);
        reopened.FinancialStatus.Should().Be("AwaitingPayment");
    }

    [Theory]
    [InlineData(GatewayResultKinds.Unknown)]
    [InlineData(GatewayResultKinds.ReverseFailed)]
    public void AmbiguousCurrentPaymentRequiresReview(string kind)
    {
        var cart = QuotedCart();
        var topUp = TopUp();
        Set(topUp, nameof(TopUpRequest.GatewayResultKind), kind);
        cart.AwaitTopUp(topUp.Id.Value);
        topUp.AttachToCart(cart.Id);
        cart.Fail();
        Map([cart], [topUp]).Single().Details.FinancialStatus.Should().Be("RequiresReview");
    }

    [Fact]
    public void OpenReviewAndForeignLinksRequireReviewWithoutLeakingOtherBuyersData()
    {
        var cart = QuotedCart();
        var topUp = TopUp();
        topUp.OpenFinancialReview("case", "reason");
        cart.AwaitTopUp(topUp.Id.Value);
        topUp.AttachToCart(cart.Id);
        Map([cart], [topUp]).Single().Details.FinancialStatus.Should().Be("RequiresReview");
        Set(topUp, nameof(TopUpRequest.UserId), UserId.New());
        var order = Map([cart], [topUp]).Single().Details;
        order.FinancialStatus.Should().Be("RequiresReview");
        order.TopUps.Should().BeEmpty();
    }

    [Fact]
    public void PaidCartMustHaveCompletePurchasesAndNonPaidCartCannotHideCommittedPurchase()
    {
        var cart = QuotedCart();
        var purchase = Purchase("a", 0, cart);
        Map([cart], purchases: [purchase]).Single().Details.FinancialStatus.Should().Be("RequiresReview");
        cart.MarkPaid();
        Map([cart]).Single().Details.FinancialStatus.Should().Be("RequiresReview");
        Map([cart], purchases: [purchase]).Single().Details.FinancialStatus.Should().Be("RequiresReview");
    }

    [Fact]
    public void PreviousFailedTopUpCannotUndoPaidCart()
    {
        var cart = QuotedCart();
        var topUp = TopUp();
        cart.AwaitTopUp(topUp.Id.Value);
        topUp.AttachToCart(cart.Id);
        topUp.Reject("رد آزمایشی");
        cart.Fail();
        cart.RecordSynchronization(true, null);
        cart.Reopen();
        Quote(cart);
        cart.MarkPaid();
        var order = Map([cart], [topUp], [Purchase("a", 0, cart), Purchase("b", 30, cart)]).Single().Details;
        order.FinancialStatus.Should().Be("Paid");
        order.TopUps.Single().Status.Should().Be("Rejected");
    }

    [Fact]
    public void LegacyTopUpGroupsOnlyDeterministicOwnedPurchasesAndKeepsStableId()
    {
        var topUp = TopUp([new PendingItemSnapshot(PendingItemType.Course, "legacy", 20)]);
        var unrelated = Purchase("legacy", 20);
        var before = Map(topUps: [topUp], purchases: [unrelated]);
        before.Should().HaveCount(2);
        var id = before.Single(row => row.Details.SourceType == "TopUpCheckout").Details.OrderId;
        topUp.Confirm("legacy-confirm");
        Map(topUps: [topUp]).Single().Details.FinancialStatus.Should().Be("RequiresReview");
        var linked = Purchase("legacy", 20, purchaseId: LegacyId(topUp, "legacy"));
        var after = Map(topUps: [topUp], purchases: [linked, unrelated]);
        after.Should().HaveCount(2);
        var order = after.Single(row => row.Details.OrderId == id).Details;
        order.FinancialStatus.Should().Be("Paid");
        order.Items.Single().PurchaseId.Should().Be(linked.Id.Value);
        order.Items.Single().Title.Should().Be("عنوان خرید legacy");
        Set(linked, nameof(CoursePurchase.BuyerId), UserId.New());
        Map(topUps: [topUp], purchases: [linked]).Should().HaveCount(2);
    }

    [Fact]
    public void IndependentTopUpIsExcludedAndStandalonePurchasesAreNotGroupedByTransaction()
    {
        var first = Purchase("a", 10);
        var second = Purchase("b", 20);
        Set(second, nameof(CoursePurchase.LedgerTransactionId), first.LedgerTransactionId);
        var pending = CoursePurchase.Create(_buyer.Id, ProductSnapshot.Create("pending", "در انتظار", 10));
        var rows = Map(topUps: [TopUp()], purchases: [first, second, pending]);
        rows.Should().HaveCount(3);
        rows.Should().OnlyContain(row => row.Details.SourceType == "Purchase");
        rows.Single(row => row.Details.OrderId.EndsWith(pending.Id.Value.ToString("D")))
            .Details.FinancialStatus.Should().Be("Processing");
    }

    [Fact]
    public void ElapsedTimeDoesNotExpireCartDuringReadAndUpdatesAreStable()
    {
        var cart = QuotedCart();
        Set(cart, nameof(Cart.ExpiresAtUtc), DateTime.UtcNow.AddDays(-1));
        var first = Map([cart]).Single().Details;
        var second = Map([cart]).Single().Details;
        first.FinancialStatus.Should().Be("AwaitingPayment");
        first.Should().BeEquivalentTo(second);
        cart.Fail(expired: true);
        Map([cart]).Single().Details.FinancialStatus.Should().Be("Expired");
    }

    [Fact]
    public async Task HandlerFiltersAfterGroupingAndUsesJwtOwnerWithStablePagination()
    {
        var cart = QuotedCart();
        var purchase = Purchase("standalone", 15);
        Set(purchase, nameof(CoursePurchase.CreatedAtUtc), cart.CreatedAtUtc);
        var snapshot = Snapshot([cart], [], [purchase]);
        var reader = new Mock<ICourseOrderReader>(MockBehavior.Strict);
        reader.Setup(service => service.ReadAsync(_buyer.Id.Value, null, default)).ReturnsAsync(snapshot);
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(service => service.UserId).Returns(_buyer.Id.Value);
        var handler = new GetMyCourseOrdersQueryHandler(reader.Object, current.Object);
        var page = await handler.Handle(new GetMyCourseOrdersQuery(new CourseOrderFilter(PageSize: 1)), default);
        page.TotalCount.Should().Be(2);
        page.Items.Single().SourceType.Should().Be("Purchase");
        var filtered = await handler.Handle(new GetMyCourseOrdersQuery(new CourseOrderFilter(
            FromUtc: cart.CreatedAtUtc, ToUtc: cart.CreatedAtUtc.AddTicks(1), FinancialStatus: "AwaitingPayment")), default);
        filtered.TotalCount.Should().Be(1);
        (await handler.Handle(new GetMyCourseOrdersQuery(new CourseOrderFilter(
            ToUtc: cart.CreatedAtUtc)), default)).Items.Should().BeEmpty();
        var missing = () => new GetMyCourseOrderQueryHandler(reader.Object, current.Object).Handle(new GetMyCourseOrderQuery($"cart:{Guid.NewGuid():D}"), default);
        await missing.Should().ThrowAsync<NotFoundException>();
        reader.VerifyAll();
    }

    [Theory]
    [InlineData("3", null, null)]
    [InlineData(null, "Cancelled", null)]
    [InlineData(null, null, "cart")]
    public async Task InvalidStatusAndSourceFiltersAreRejected(string? financial, string? delivery, string? source)
    {
        var validator = new GetMyCourseOrdersQueryValidator();
        var result = await validator.ValidateAsync(new GetMyCourseOrdersQuery(new CourseOrderFilter(
            FinancialStatus: financial, DeliveryStatus: delivery, SourceType: source)));
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("cart:00000000-0000-0000-0000-000000000000")]
    [InlineData("cart:ABCDEF01-2345-6789-ABCD-0123456789AB")]
    [InlineData("purchase:garbage")]
    [InlineData("unknown:abcdef01-2345-6789-abcd-0123456789ab")]
    public void InvalidCompositeIdsAreRejected(string value) => CourseOrderId.IsValid(value).Should().BeFalse();

    [Fact]
    public async Task MissingIdentityDoesNotReadDataAndAdminEndpointRequiresReadPermission()
    {
        var reader = new Mock<ICourseOrderReader>(MockBehavior.Strict);
        var handler = new GetMyCourseOrdersQueryHandler(reader.Object, Mock.Of<ICurrentUserService>());
        var action = () => handler.Handle(new GetMyCourseOrdersQuery(new CourseOrderFilter()), default);
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        reader.VerifyNoOtherCalls();
        typeof(AdminCourseOrdersController).GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be(PolicyNames.RequirePurchasesView);
        typeof(MyCourseOrdersController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
    }

    [Fact]
    public async Task AdminDetailsShowCancelledAttemptWithBuyerAndAdminListCountsAttempts()
    {
        var cart = QuotedCart();
        var topUp = TopUp();
        cart.AwaitTopUp(topUp.Id.Value);
        topUp.AttachToCart(cart.Id);
        topUp.Reject("پرداخت بانکی انجام نشده است.");
        var snapshot = Snapshot([cart], [topUp], []);
        var reader = new Mock<ICourseOrderReader>();
        reader.Setup(item => item.ReadAsync(null, null, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
        var orderId = CourseOrderId.For(CourseOrderSourceType.Cart, cart.Id);

        var details = await new GetAdminCourseOrderQueryHandler(reader.Object, user.Object)
            .Handle(new GetAdminCourseOrderQuery(orderId), default);

        details.BuyerExternalUserId.Should().Be(_buyer.ExternalUserId);
        details.Order.FinancialStatus.Should().Be("Failed");
        details.Order.TopUps.Should().ContainSingle().Which.Status.Should().Be("Rejected");
        details.Order.Items.Should().HaveCount(2);

        var page = await new GetAdminCourseOrdersQueryHandler(reader.Object, user.Object)
            .Handle(new GetAdminCourseOrdersQuery(new AdminCourseOrderFilter(FinancialStatus: "Failed")), default);
        page.Items.Should().ContainSingle().Which.TopUpAttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task AdminDetailsRejectsMissingIdentityUnknownOrderAndMalformedId()
    {
        var reader = new Mock<ICourseOrderReader>();
        reader.Setup(item => item.ReadAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Snapshot([], [], []));
        var anonymous = new GetAdminCourseOrderQueryHandler(reader.Object, Mock.Of<ICurrentUserService>());
        var missingIdentity = () => anonymous.Handle(
            new GetAdminCourseOrderQuery(CourseOrderId.For(CourseOrderSourceType.Cart, Guid.NewGuid())), default);
        await missingIdentity.Should().ThrowAsync<UnauthorizedAccessException>();

        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
        var unknown = () => new GetAdminCourseOrderQueryHandler(reader.Object, user.Object).Handle(
            new GetAdminCourseOrderQuery(CourseOrderId.For(CourseOrderSourceType.Cart, Guid.NewGuid())), default);
        await unknown.Should().ThrowAsync<NotFoundException>();

        (await new GetAdminCourseOrderQueryValidator().ValidateAsync(new GetAdminCourseOrderQuery("garbage")))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task InvalidPagingDateRangeAndBuyerFilterAreRejected()
    {
        var validator = new GetMyCourseOrdersQueryValidator();
        (await validator.ValidateAsync(new GetMyCourseOrdersQuery(new CourseOrderFilter(Page: int.MaxValue))))
            .IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new GetMyCourseOrdersQuery(new CourseOrderFilter(PageSize: 101))))
            .IsValid.Should().BeFalse();
        var time = DateTimeOffset.UtcNow;
        (await validator.ValidateAsync(new GetMyCourseOrdersQuery(new CourseOrderFilter(FromUtc: time, ToUtc: time))))
            .IsValid.Should().BeFalse();
        var adminValidator = new GetAdminCourseOrdersQueryValidator();
        (await adminValidator.ValidateAsync(new GetAdminCourseOrdersQuery(new AdminCourseOrderFilter(BuyerExternalUserId: " "))))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void ReadyCartWithSameIdForCourseAndProductMapsBothItemsWithoutThrowing()
    {
        var cart = SameIdCart(productQuantity: 2, productCouponDiscount: 10);

        var order = Map([cart]).Single().Details;

        order.FinancialStatus.Should().Be("AwaitingPayment");
        order.ItemCount.Should().Be(2);
        order.Items.Select(item => item.ItemType).Should()
            .BeEquivalentTo(nameof(ProductItemType.Course), nameof(ProductItemType.Product));
        order.Items.Single(item => item.ItemType == nameof(ProductItemType.Product)).Quantity.Should().Be(2);
        order.CartSummary.Should().NotBeNull();
    }

    [Fact]
    public void PaidCartWithSameIdForCourseAndProductIsPaidWithBothItemsPaid()
    {
        var cart = SameIdCart(productQuantity: 2, productCouponDiscount: 10);
        cart.MarkPaid();
        var course = Purchase("1", 100, cart);
        var product = PhysicalPurchase(cart, "1", quantity: 2, unitPrice: 50, paid: 90);

        var order = Map([cart], purchases: [course], physicalPurchases: [product]).Single().Details;

        order.FinancialStatus.Should().Be("Paid");
        order.Items.Should().HaveCount(2);
        order.Items.Should().OnlyContain(item => item.FinancialStatus == "Paid" && item.PurchaseId != null);
        order.Items.Single(item => item.ItemType == nameof(ProductItemType.Product))
            .FinalAmountInNoor.Should().Be(90);
        order.Items.Single(item => item.ItemType == nameof(ProductItemType.Course))
            .FinalAmountInNoor.Should().Be(100);
    }

    [Fact]
    public void FreeProductInPaidCartIsConsistentAndPaid()
    {
        var cart = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId,
            [new CartItemInput("free-product", ProductItemType.Product, 1)], null);
        cart.SetQuote([new CartQuoteItem("free-product", "کالای رایگان", 50, 0, 50, 0, ProductItemType.Product)]);
        cart.MarkPaid();
        var product = PhysicalPurchase(cart, "free-product", quantity: 1, unitPrice: 50, paid: 0);

        var order = Map([cart], physicalPurchases: [product]).Single().Details;

        order.FinancialStatus.Should().Be("Paid");
        order.Items.Single().FinalAmountInNoor.Should().Be(0);
        order.FinalAmountInNoor.Should().Be(0);
    }

    [Theory]
    [InlineData(3, 50, 90)]
    [InlineData(2, 60, 90)]
    [InlineData(2, 50, 80)]
    public void PaidProductThatDiffersFromCartItemRequiresReview(int quantity, decimal unitPrice, decimal paid)
    {
        var cart = SameIdCart(productQuantity: 2, productCouponDiscount: 10);
        cart.MarkPaid();
        var course = Purchase("1", 100, cart);
        var product = PhysicalPurchase(cart, "1", quantity, unitPrice, paid);

        var order = Map([cart], purchases: [course], physicalPurchases: [product]).Single().Details;

        order.FinancialStatus.Should().Be("RequiresReview");
    }

    [Fact]
    public void PaidPurchaseWithDifferentTypeThanCartItemRequiresReview()
    {
        var courseCart = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId,
            [new CartItemInput("1", ProductItemType.Course, 1)], null);
        courseCart.SetQuote([new CartQuoteItem("1", "دوره", 100, 0, 0, 100)]);
        courseCart.MarkPaid();
        var productInsteadOfCourse = PhysicalPurchase(courseCart, "1", quantity: 1, unitPrice: 100, paid: 100);
        Map([courseCart], physicalPurchases: [productInsteadOfCourse]).Single().Details
            .FinancialStatus.Should().Be("RequiresReview");

        var productCart = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId,
            [new CartItemInput("1", ProductItemType.Product, 1)], null);
        productCart.SetQuote([new CartQuoteItem("1", "کالا", 100, 0, 0, 100, ProductItemType.Product)]);
        productCart.MarkPaid();
        var courseInsteadOfProduct = Purchase("1", 100, productCart);
        Map([productCart], purchases: [courseInsteadOfProduct]).Single().Details
            .FinancialStatus.Should().Be("RequiresReview");
    }

    [Fact]
    public async Task OrderOfAnotherUserIsNotFoundForCurrentUser()
    {
        var otherCart = Cart.Create(Guid.NewGuid(), Guid.NewGuid(), "other-buyer", ["a"], null);
        var reader = new Mock<ICourseOrderReader>(MockBehavior.Strict);
        reader.Setup(service => service.ReadAsync(_buyer.Id.Value, null, default))
            .ReturnsAsync(Snapshot([], [], []));
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(service => service.UserId).Returns(_buyer.Id.Value);

        var action = () => new GetMyCourseOrderQueryHandler(reader.Object, current.Object).Handle(
            new GetMyCourseOrderQuery(CourseOrderId.For(CourseOrderSourceType.Cart, otherCart.Id)), default);

        await action.Should().ThrowAsync<NotFoundException>();
        reader.VerifyAll();
    }

    [Fact]
    public async Task AdminHandlersListAndShowCartWithSameIdForCourseAndProduct()
    {
        var cart = SameIdCart(productQuantity: 2, productCouponDiscount: 10);
        cart.MarkPaid();
        var snapshot = Snapshot([cart], [], [Purchase("1", 100, cart)],
            [PhysicalPurchase(cart, "1", quantity: 2, unitPrice: 50, paid: 90)]);
        var reader = new Mock<ICourseOrderReader>();
        reader.Setup(item => item.ReadAsync(null, null, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.UserId).Returns(Guid.NewGuid());

        var page = await new GetAdminCourseOrdersQueryHandler(reader.Object, user.Object)
            .Handle(new GetAdminCourseOrdersQuery(new AdminCourseOrderFilter()), default);
        var details = await new GetAdminCourseOrderQueryHandler(reader.Object, user.Object).Handle(
            new GetAdminCourseOrderQuery(CourseOrderId.For(CourseOrderSourceType.Cart, cart.Id)), default);

        page.Items.Should().ContainSingle().Which.ItemCount.Should().Be(2);
        details.Order.FinancialStatus.Should().Be("Paid");
        details.Order.Items.Should().HaveCount(2);
    }

    private Cart SameIdCart(int productQuantity, decimal productCouponDiscount)
    {
        var cart = Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, [
            new CartItemInput("1", ProductItemType.Course, 1),
            new CartItemInput("1", ProductItemType.Product, productQuantity)
        ], null);
        var productLineTotal = productQuantity * 50m;
        cart.SetQuote([
            new CartQuoteItem("1", "دوره", 100, 0, 0, 100),
            new CartQuoteItem("1", "کالا", 50, 0, productCouponDiscount,
                productLineTotal - productCouponDiscount, ProductItemType.Product, Quantity: productQuantity)
        ]);
        return cart;
    }

    private PhysicalProductPurchase PhysicalPurchase(
        Cart cart, string productId, int quantity, decimal unitPrice, decimal paid)
    {
        var purchase = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(), cart.Id, _buyer.Id, productId, "کالا", quantity, unitPrice, paid);
        if (paid == 0)
        {
            purchase.MarkFree(cart.Id);
        }
        else
        {
            purchase.MarkDebited(Guid.NewGuid());
        }

        return purchase;
    }

    private Cart CreateCart(params string[] ids) =>
        Cart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, ids, null);

    private Cart QuotedCart()
    {
        var cart = CreateCart("a", "b");
        Quote(cart);
        return cart;
    }

    private static void Quote(Cart cart) => cart.SetQuote([
        new CartQuoteItem("a", "دوره الف", 100, 0, 100, 0),
        new CartQuoteItem("b", "دوره ب", 40, 10, 0, 30)]);

    private TopUpRequest TopUp(IReadOnlyList<PendingItemSnapshot>? items = null) =>
        TopUpRequest.CreateGateway(_buyer.Id, 20, 1000, items);

    private CoursePurchase Purchase(string course, decimal amount, Cart? cart = null, CoursePurchaseId? purchaseId = null)
    {
        var purchase = CoursePurchase.Create(_buyer.Id,
            ProductSnapshot.Create(course, $"عنوان خرید {course}", amount, isFree: amount == 0),
            isFree: amount == 0, purchaseId: purchaseId);
        if (cart is not null)
        {
            purchase.AttachToCart(cart.Id);
        }
        purchase.MarkDebited(Guid.NewGuid());
        return purchase;
    }

    private IReadOnlyList<CourseOrderReadRow> Map(
        Cart[]? carts = null, TopUpRequest[]? topUps = null, CoursePurchase[]? purchases = null,
        PhysicalProductPurchase[]? physicalPurchases = null) =>
        CourseOrderMapping.Build(Snapshot(carts ?? [], topUps ?? [], purchases ?? [], physicalPurchases ?? []));

    private CourseOrderSnapshot Snapshot(
        Cart[] carts, TopUpRequest[] topUps, CoursePurchase[] purchases,
        PhysicalProductPurchase[]? physicalPurchases = null) =>
        new(carts, topUps, purchases, [new CourseOrderBuyer(_buyer.Id.Value, _buyer.ExternalUserId, null)],
            physicalPurchases ?? []);

    private static CoursePurchaseId LegacyId(TopUpRequest topUp, string course) => new(new Guid(
        SHA256.HashData(Encoding.UTF8.GetBytes($"{topUp.Id.Value:N}:{course}")).AsSpan(0, 16)));

    private static void Set(object entity, string property, object? value) =>
        entity.GetType().GetProperty(property)!.DeclaringType!.GetProperty(property)!.SetValue(entity, value);
}
