using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
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
        var cart = Cart("a", "b");
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
        cart.Fail();
        Map([cart], [topUp]).Single().Details.FinancialStatus.Should().Be("RequiresReview");
    }

    [Fact]
    public void OpenReviewAndMissingOrForeignLinksRequireReviewWithoutLeakingOtherBuyersData()
    {
        var cart = QuotedCart();
        var topUp = TopUp();
        topUp.OpenFinancialReview("case", "reason");
        cart.AwaitTopUp(topUp.Id.Value);
        Map([cart], [topUp]).Single().Details.FinancialStatus.Should().Be("RequiresReview");
        Map([cart]).Single().Details.FinancialStatus.Should().Be("RequiresReview");
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
        Set(cart, nameof(CourseCart.ExpiresAtUtc), DateTime.UtcNow.AddDays(-1));
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

    private CourseCart Cart(params string[] ids) =>
        CourseCart.Create(Guid.NewGuid(), _buyer.Id.Value, _buyer.ExternalUserId, ids, null);

    private CourseCart QuotedCart()
    {
        var cart = Cart("a", "b");
        Quote(cart);
        return cart;
    }

    private static void Quote(CourseCart cart) => cart.SetQuote([
        new CourseCartQuoteItem("a", "دوره الف", 100, 0, 100, 0),
        new CourseCartQuoteItem("b", "دوره ب", 40, 10, 0, 30)]);

    private TopUpRequest TopUp(IReadOnlyList<PendingItemSnapshot>? items = null) =>
        TopUpRequest.CreateGateway(_buyer.Id, 20, 1000, items);

    private CoursePurchase Purchase(string course, decimal amount, CourseCart? cart = null, CoursePurchaseId? purchaseId = null)
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
        CourseCart[]? carts = null, TopUpRequest[]? topUps = null, CoursePurchase[]? purchases = null) =>
        CourseOrderMapping.Build(Snapshot(carts ?? [], topUps ?? [], purchases ?? []));

    private CourseOrderSnapshot Snapshot(CourseCart[] carts, TopUpRequest[] topUps, CoursePurchase[] purchases) =>
        new(carts, topUps, purchases, [new CourseOrderBuyer(_buyer.Id.Value, _buyer.ExternalUserId, null)]);

    private static CoursePurchaseId LegacyId(TopUpRequest topUp, string course) => new(new Guid(
        SHA256.HashData(Encoding.UTF8.GetBytes($"{topUp.Id.Value:N}:{course}")).AsSpan(0, 16)));

    private static void Set(object entity, string property, object? value) =>
        entity.GetType().GetProperty(property)!.DeclaringType!.GetProperty(property)!.SetValue(entity, value);
}
