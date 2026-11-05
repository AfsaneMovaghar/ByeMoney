using System.Reflection;
using ByeMoney.API.Controllers;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Application.Modules.Purchases.Commands.UpdatePhysicalDeliveryStatus;
using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.History.Services;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminCourseOrders;
using ByeMoney.Application.Modules.Purchases.Queries.GetMyCourseOrderReceipt;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Constants;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Moq;

namespace ByeMoney.UnitTests;

public sealed class PhysicalDeliveryAndOrderReceiptTests
{
    private const decimal CourseAmount = 80m;
    private const decimal ProductAmount = 70m;
    private const decimal Shipping = 25m;
    private const decimal CartTotal = 175m;

    [Fact]
    public void NewPhysicalPurchaseStartsPreparingAndMovesForwardOnly()
    {
        var purchase = CreatePhysical(ProductAmount);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);

        var skip = () => purchase.MarkDelivered();
        skip.Should().Throw<DomainException>();

        purchase.MarkShipped();
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        purchase.ShippedAtUtc.Should().NotBeNull();
        purchase.DeliveredAtUtc.Should().BeNull();

        var shippedAt = purchase.ShippedAtUtc;
        purchase.MarkShipped();
        purchase.ShippedAtUtc.Should().Be(shippedAt);

        purchase.MarkDelivered();
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Delivered);
        purchase.DeliveredAtUtc.Should().NotBeNull();

        var back = () => purchase.MarkShipped();
        back.Should().Throw<DomainException>();
        purchase.MarkDelivered();
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Delivered);
    }

    [Fact]
    public void DeliveryChangeDoesNotTouchMoneyOrLedgerReference()
    {
        var paid = PayMixedCart();
        var physical = paid.Payment.PhysicalPurchases.Single();
        var balanceBefore = paid.Wallet.WithdrawableBalance;
        var ledgerCountBefore = paid.Payment.LedgerEntries.Count;

        physical.MarkShipped();
        physical.MarkDelivered();

        paid.Wallet.WithdrawableBalance.Should().Be(balanceBefore).And.Be(825m);
        paid.Payment.LedgerEntries.Should().HaveCount(ledgerCountBefore);
        physical.PaidAmountInNoor.Should().Be(ProductAmount);
        physical.LedgerTransactionId.Should().Be(paid.Cart.Id);
    }

    [Fact]
    public void PaidMixedCartIsPaidWithBothItemTypesAndShippingBalances()
    {
        var paid = PayMixedCart();

        var order = CourseOrderMapping.Build(Snapshot(paid)).Single().Details;

        order.FinancialStatus.Should().Be("Paid");
        order.DeliveryStatus.Should().Be("Pending");
        order.FinalAmountInNoor.Should().Be(CartTotal);
        order.CartSummary!.ShippingAmountInNoor.Should().Be(Shipping);
        order.CartSummary.OriginalTotalInNoor.Should().Be(200m);
        order.Items.Select(item => item.ItemType).Should().Equal("Course", "Product");
        var product = order.Items.Single(item => item.ItemType == "Product");
        product.Quantity.Should().Be(2);
        product.PriceInNoor.Should().Be(50m);
        product.FinalAmountInNoor.Should().Be(ProductAmount);
        product.PhysicalDeliveryStatus.Should().Be("Preparing");
        product.DeliveryStatus.Should().Be("Pending");
        order.Items.Single(item => item.ItemType == "Course").PhysicalDeliveryStatus.Should().BeNull();
        (order.Items.Sum(item => item.FinalAmountInNoor!.Value) + order.CartSummary.ShippingAmountInNoor)
            .Should().Be(order.FinalAmountInNoor);
    }

    [Fact]
    public void OrderDeliveryFollowsPhysicalStatusAndCourseNotification()
    {
        var paid = PayMixedCart();
        var physical = paid.Payment.PhysicalPurchases.Single();
        var course = paid.Payment.Purchases.Single();

        physical.MarkShipped();
        var shipped = CourseOrderMapping.Build(Snapshot(paid)).Single().Details;
        shipped.DeliveryStatus.Should().Be("Pending");
        shipped.Items.Single(item => item.ItemType == "Product").PhysicalDeliveryStatus.Should().Be("Shipped");
        shipped.Items.Single(item => item.ItemType == "Product").DeliveryStatus.Should().Be("Pending");

        physical.MarkDelivered();
        var partial = CourseOrderMapping.Build(Snapshot(paid)).Single().Details;
        partial.DeliveryStatus.Should().Be("PartiallyDelivered");
        partial.Items.Single(item => item.ItemType == "Product").DeliveryStatus.Should().Be("Delivered");

        course.MarkNotificationSent();
        CourseOrderMapping.Build(Snapshot(paid)).Single().Details.DeliveryStatus.Should().Be("Delivered");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void OrderItemAndReceiptExposeCancelledQuantityWithoutChangingStatuses(int cancelled)
    {
        var paid = PayMixedCart();
        var physical = paid.Payment.PhysicalPurchases.Single();
        if (cancelled > 0)
        {
            physical.RecordPaymentShares(new PaymentShares(ProductAmount - 20m, 20m));
            physical.RecordCancellation(cancelled, new PaymentShares(cancelled * 5m, cancelled * 3m));
        }

        var snapshot = Snapshot(paid);
        var order = CourseOrderMapping.Build(snapshot).Single().Details;
        var product = order.Items.Single(item => item.ItemType == "Product");
        var course = order.Items.Single(item => item.ItemType == "Course");

        order.FinancialStatus.Should().Be("Paid");
        product.FinancialStatus.Should().Be("Paid");
        product.Quantity.Should().Be(2);
        product.CancelledQuantity.Should().Be(cancelled);
        product.RefundedRegularInNoor.Should().Be(cancelled * 5m);
        product.RefundedGiftInNoor.Should().Be(cancelled * 3m);
        product.PhysicalDeliveryStatus.Should().Be("Preparing");
        course.CancelledQuantity.Should().Be(0);
        course.RefundedRegularInNoor.Should().Be(0m);
        course.RefundedGiftInNoor.Should().Be(0m);

        var receipt = CourseOrderReceiptMapping.Build(order, snapshot)!;
        var receiptProduct = receipt.Items.Single(item => item.ItemType == "Product");
        receiptProduct.CancelledQuantity.Should().Be(cancelled);
        receiptProduct.RefundedRegularInNoor.Should().Be(cancelled * 5m);
        receiptProduct.RefundedGiftInNoor.Should().Be(cancelled * 3m);
        receipt.Items.Single(item => item.ItemType == "Course").CancelledQuantity.Should().Be(0);
    }

    [Fact]
    public void MissingPhysicalPurchaseOfPaidCartRequiresReview()
    {
        var paid = PayMixedCart();
        var snapshot = new CourseOrderSnapshot(
            [paid.Cart], [], paid.Payment.Purchases, [], [], []);

        CourseOrderMapping.Build(snapshot).Single().Details.FinancialStatus.Should().Be("RequiresReview");
    }

    [Fact]
    public void OrderReceiptCoversAllItemsAndShippingWithBalancedAmounts()
    {
        var paid = PayMixedCart();
        var physical = paid.Payment.PhysicalPurchases.Single();
        physical.MarkShipped();
        physical.MarkDelivered();
        var snapshot = Snapshot(paid);
        var order = CourseOrderMapping.Build(snapshot).Single().Details;

        var receipt = CourseOrderReceiptMapping.Build(order, snapshot)!;

        receipt.OrderId.Should().Be(order.OrderId);
        receipt.TransactionId.Should().Be(paid.Cart.Id);
        receipt.FinalAmountInNoor.Should().Be(CartTotal);
        receipt.CartSummary!.ShippingAmountInNoor.Should().Be(Shipping);
        receipt.CouponCode.Should().Be("DISCOUNT");
        receipt.Items.Should().HaveCount(2);
        (receipt.Items.Sum(item => item.FinalAmountInNoor) + receipt.CartSummary.ShippingAmountInNoor)
            .Should().Be(receipt.FinalAmountInNoor);

        var product = receipt.Items.Single(item => item.ItemType == "Product");
        product.PurchaseId.Should().Be(physical.Id.Value);
        product.Quantity.Should().Be(2);
        product.BaseAmountInNoor.Should().Be(100m);
        product.DiscountAmountInNoor.Should().Be(30m);
        product.FinalAmountInNoor.Should().Be(ProductAmount);
        product.PhysicalDeliveryStatus.Should().Be("Delivered");
        product.DeliveredAtUtc.Should().Be(physical.DeliveredAtUtc);
        product.ReferenceRialPerNoorAtPurchaseTime.Should().BeNull();

        var courseItem = receipt.Items.Single(item => item.ItemType == "Course");
        courseItem.BaseAmountInNoor.Should().Be(100m);
        courseItem.FinalAmountInNoor.Should().Be(CourseAmount);
        courseItem.PhysicalDeliveryStatus.Should().BeNull();
    }

    [Fact]
    public void UnquotedCartKeepsRequestedItemTypeAndQuantity()
    {
        var buyer = new UserId(Guid.NewGuid());
        var cart = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer-ext", [
            new CartItemInput("product-1", ProductItemType.Product, 2)
        ], null);
        var snapshot = new CourseOrderSnapshot([cart], [], [], [], [], []);

        var item = CourseOrderMapping.Build(snapshot).Single().Details.Items.Single();

        item.ItemType.Should().Be("Product");
        item.Quantity.Should().Be(2);
    }

    [Fact]
    public void OrderReceiptIsUnavailableUntilOrderIsPaid()
    {
        var paid = PayMixedCart();
        var snapshot = new CourseOrderSnapshot([paid.Cart], [], paid.Payment.Purchases, [], [], []);
        var incomplete = CourseOrderMapping.Build(snapshot).Single().Details;

        CourseOrderReceiptMapping.Build(incomplete, snapshot).Should().BeNull();
    }

    [Fact]
    public void SinglePhysicalReceiptReflectsRealDeliveryAndShipping()
    {
        var paid = PayMixedCart();
        var physical = paid.Payment.PhysicalPurchases.Single();

        var preparing = PurchaseHistoryMapping.ToReceipt(new PurchaseReceiptData(null, paid.Cart, physical));
        preparing.DeliveryStatus.Should().Be("Pending");
        preparing.DeliveredAtUtc.Should().BeNull();
        preparing.CartSummary!.ShippingAmountInNoor.Should().Be(Shipping);

        physical.MarkShipped();
        PurchaseHistoryMapping.ToReceipt(new PurchaseReceiptData(null, paid.Cart, physical))
            .DeliveryStatus.Should().Be("Pending");

        physical.MarkDelivered();
        var delivered = PurchaseHistoryMapping.ToReceipt(new PurchaseReceiptData(null, paid.Cart, physical));
        delivered.DeliveryStatus.Should().Be("Delivered");
        delivered.DeliveredAtUtc.Should().Be(physical.DeliveredAtUtc);
    }

    [Theory]
    [InlineData(PhysicalDeliveryStatus.Preparing, "Pending")]
    [InlineData(PhysicalDeliveryStatus.Shipped, "Pending")]
    [InlineData(PhysicalDeliveryStatus.Delivered, "Delivered")]
    public void PurchaseListRowUsesPhysicalDeliveryStatus(PhysicalDeliveryStatus status, string expected)
    {
        var row = new PurchaseReadRow(
            Guid.NewGuid(), Guid.NewGuid(), "Book", ProductAmount, DateTime.UtcNow,
            CoursePurchaseStatus.Debited, "buyer", null, null, ProductItemType.Product, null, status);

        PurchaseHistoryMapping.ToListItem(row).DeliveryStatus.Should().Be(expected);
        PurchaseHistoryMapping.ToAdminListItem(row).DeliveryStatus.Should().Be(expected);
    }

    [Theory]
    [InlineData("Shipped", true)]
    [InlineData("Delivered", true)]
    [InlineData("Preparing", false)]
    [InlineData("shipped", false)]
    [InlineData("", false)]
    public async Task DeliveryCommandValidatorAcceptsOnlyForwardTargets(string status, bool valid)
    {
        var validator = new UpdatePhysicalDeliveryStatusCommandValidator();

        var result = await validator.ValidateAsync(new UpdatePhysicalDeliveryStatusCommand(Guid.NewGuid(), status));

        result.IsValid.Should().Be(valid);
        (await validator.ValidateAsync(new UpdatePhysicalDeliveryStatusCommand(Guid.Empty, "Shipped")))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task DeliveryCommandHandlerUpdatesAndSavesOrReportsNotFound()
    {
        var physical = CreatePhysical(ProductAmount);
        var repository = new Mock<IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        repository.Setup(item => item.GetByIdAsync(physical.Id, It.IsAny<CancellationToken>())).ReturnsAsync(physical);
        repository.Setup(item => item.GetByIdAsync(It.Is<PhysicalProductPurchaseId>(id => id != physical.Id),
            It.IsAny<CancellationToken>())).ReturnsAsync((PhysicalProductPurchase?)null);
        var deliveryStore = new Mock<IPhysicalDeliveryStore>();
        var fresh = CreatePhysical(ProductAmount);
        var knownPurchaseIds = new[] { physical.Id.Value, fresh.Id.Value };
        deliveryStore
            .Setup(item => item.FindOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                knownPurchaseIds.Contains(id) ? new PhysicalPurchaseOwner(Guid.NewGuid(), Guid.NewGuid()) : null);
        deliveryStore
            .Setup(item => item.ExecuteLockedAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Func<Task<PhysicalDeliveryStatusResponse>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Guid _, Guid _, Func<Task<PhysicalDeliveryStatusResponse>> work, CancellationToken _) => work());
        var handler = new UpdatePhysicalDeliveryStatusCommandHandler(
            repository.Object, deliveryStore.Object, unitOfWork.Object);

        var response = await handler.Handle(
            new UpdatePhysicalDeliveryStatusCommand(physical.Id.Value, "Shipped"), CancellationToken.None);

        response.DeliveryStatus.Should().Be("Shipped");
        response.ShippedAtUtc.Should().NotBeNull();
        repository.Verify(item => item.Update(physical), Times.Once);
        unitOfWork.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        var missing = () => handler.Handle(
            new UpdatePhysicalDeliveryStatusCommand(Guid.NewGuid(), "Shipped"), CancellationToken.None);
        await missing.Should().ThrowAsync<NotFoundException>();

        var deliver = () => handler.Handle(
            new UpdatePhysicalDeliveryStatusCommand(physical.Id.Value, "Delivered"), CancellationToken.None);
        (await deliver.Should().NotThrowAsync()).Subject.DeliveryStatus.Should().Be("Delivered");

        repository.Setup(item => item.GetByIdAsync(fresh.Id, It.IsAny<CancellationToken>())).ReturnsAsync(fresh);
        var skip = () => handler.Handle(
            new UpdatePhysicalDeliveryStatusCommand(fresh.Id.Value, "Delivered"), CancellationToken.None);
        await skip.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task OrderReceiptHandlerReturnsNotFoundForUnknownOrUnpaidOrder()
    {
        var paid = PayMixedCart();
        var snapshot = new CourseOrderSnapshot([paid.Cart], [], paid.Payment.Purchases, [], [], []);
        var reader = new Mock<ICourseOrderReader>();
        reader.Setup(item => item.ReadAsync(It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.UserId).Returns(paid.Cart.BuyerId.Value);
        var handler = new GetMyCourseOrderReceiptQueryHandler(reader.Object, user.Object);

        var unpaid = () => handler.Handle(
            new GetMyCourseOrderReceiptQuery(CourseOrderId.For(
                Application.Modules.Purchases.Orders.Constants.CourseOrderSourceType.Cart, paid.Cart.Id)),
            CancellationToken.None);
        var unknown = () => handler.Handle(
            new GetMyCourseOrderReceiptQuery(CourseOrderId.For(
                Application.Modules.Purchases.Orders.Constants.CourseOrderSourceType.Cart, Guid.NewGuid())),
            CancellationToken.None);

        await unpaid.Should().ThrowAsync<NotFoundException>();
        await unknown.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public void DeliveryEndpointUsesExistingCoursesManagePolicyAndReceiptsKeepOrderPolicies()
    {
        typeof(AdminPhysicalPurchasesController).GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(PolicyNames.RequireCoursesManage);
        typeof(AdminCourseOrdersController).GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(PolicyNames.RequirePurchasesView);
        typeof(MyCourseOrdersController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(AdminCourseOrdersController).GetMethod(nameof(AdminCourseOrdersController.Receipt)).Should().NotBeNull();
        typeof(MyCourseOrdersController).GetMethod(nameof(MyCourseOrdersController.Receipt)).Should().NotBeNull();
    }

    private static CourseOrderSnapshot Snapshot(PaidMixedCart paid) => new(
        [paid.Cart], [], paid.Payment.Purchases, [], paid.Payment.PhysicalPurchases, []);

    [Fact]
    public async Task AdminListShowsZeroRefundForOrderWithoutCancellation()
    {
        var paid = PayMixedCart();

        var item = await ReadAdminListItemAsync(paid, []);

        item.RefundedAmountInNoor.Should().Be(0m);
        item.FinalAmountInNoor.Should().Be(CartTotal);
    }

    [Fact]
    public async Task AdminListSumsPartialCancellationsOfBothActors()
    {
        var paid = PayMixedCart();
        var physical = paid.Payment.PhysicalPurchases.Single();
        var buyerCancellation = Cancel(paid, 1, new PaymentShares(20m, 5m), new PaymentShares(0m, 0m),
            PhysicalCancellationActor.Buyer);
        var adminCancellation = Cancel(paid, 1, new PaymentShares(10m, 0m), new PaymentShares(0m, 0m),
            PhysicalCancellationActor.Admin);

        var item = await ReadAdminListItemAsync(paid, [buyerCancellation, adminCancellation]);

        item.RefundedAmountInNoor.Should().Be(35m);
        item.FinalAmountInNoor.Should().Be(CartTotal);
        physical.PaidAmountInNoor.Should().Be(ProductAmount);
    }

    [Fact]
    public async Task AdminListCountsFullCancellationAndNeverExceedsFinalAmount()
    {
        var paid = PayMixedCart();
        var full = Cancel(paid, 2, new PaymentShares(ProductAmount, 0m), new PaymentShares(0m, 0m),
            PhysicalCancellationActor.Buyer);

        var item = await ReadAdminListItemAsync(paid, [full]);
        item.RefundedAmountInNoor.Should().Be(ProductAmount);

        var excessive = Cancel(paid, 1, new PaymentShares(CartTotal, 50m), new PaymentShares(0m, 0m),
            PhysicalCancellationActor.Admin);
        var capped = await ReadAdminListItemAsync(paid, [full, excessive]);
        capped.RefundedAmountInNoor.Should().Be(CartTotal);
    }

    [Fact]
    public async Task AdminListIncludesShippingRefundOfCompletingCancellation()
    {
        var paid = PayMixedCart();
        var completing = Cancel(paid, 2, new PaymentShares(ProductAmount, 0m), new PaymentShares(Shipping - 5m, 5m),
            PhysicalCancellationActor.Admin);

        var item = await ReadAdminListItemAsync(paid, [completing]);

        item.RefundedAmountInNoor.Should().Be(ProductAmount + Shipping);
    }

    [Fact]
    public async Task AdminListDeliveryStatusIsCancelledWhenAllPhysicalQuantityIsCancelled()
    {
        var paid = PayMixedCart();
        paid.Payment.PhysicalPurchases.Single().RecordCancellation(2, new PaymentShares(ProductAmount, 0m));

        var item = await ReadAdminListItemAsync(paid, []);

        item.DeliveryStatus.Should().Be("Cancelled");
        item.FinancialStatus.Should().Be("Paid");
    }

    [Fact]
    public async Task AdminListDeliveryStatusIsKeptForPartialQuantityCancellation()
    {
        var paid = PayMixedCart();
        paid.Payment.PhysicalPurchases.Single().RecordCancellation(1, new PaymentShares(20m, 0m));

        var item = await ReadAdminListItemAsync(paid, []);

        item.DeliveryStatus.Should().Be("Pending");
    }

    private static async Task<AdminCourseOrderListItem> ReadAdminListItemAsync(
        PaidMixedCart paid, PhysicalProductCancellation[] cancellations)
    {
        var snapshot = new CourseOrderSnapshot(
            [paid.Cart], [], paid.Payment.Purchases,
            [new CourseOrderBuyer(paid.Cart.BuyerId.Value, "buyer-ext", null)],
            paid.Payment.PhysicalPurchases, cancellations);
        var reader = new Mock<ICourseOrderReader>();
        reader.Setup(item => item.ReadAsync(null, null, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.UserId).Returns(Guid.NewGuid());

        var page = await new GetAdminCourseOrdersQueryHandler(reader.Object, user.Object)
            .Handle(new GetAdminCourseOrdersQuery(new AdminCourseOrderFilter()), default);

        return page.Items.Single();
    }

    private static PhysicalProductCancellation Cancel(
        PaidMixedCart paid, int quantity, PaymentShares refund, PaymentShares shippingRefund,
        PhysicalCancellationActor actor) => PhysicalProductCancellation.Create(
        Guid.NewGuid(), paid.Payment.PhysicalPurchases.Single(), quantity, refund, shippingRefund,
        actor, paid.Cart.BuyerId, "reason");

    private static PhysicalProductPurchase CreatePhysical(decimal paid) => PhysicalProductPurchase.Create(
        PhysicalProductPurchaseId.New(), Guid.NewGuid(), new UserId(Guid.NewGuid()),
        "product-1", "Book", 2, 50m, paid);

    private static PaidMixedCart PayMixedCart()
    {
        var buyer = new UserId(Guid.NewGuid());
        var account = Account.CreateUserAccount(buyer);
        var wallet = Wallet.Create(account.Id, buyer);
        wallet.ApplyCredit(1000m);
        var cart = Cart.Create(Guid.NewGuid(), buyer.Value, "buyer-ext", [
            new CartItemInput("course-1", ProductItemType.Course, 1),
            new CartItemInput("product-1", ProductItemType.Product, 2)
        ], "DISCOUNT");
        cart.SetQuote([
            new CartQuoteItem("course-1", "Course", 100m, 10m, 10m, CourseAmount, ProductItemType.Course, null, 1),
            new CartQuoteItem("product-1", "Book", 50m, 20m, 10m, ProductAmount, ProductItemType.Product, null, 2)
        ], shippingAmountInNoor: Shipping);

        var payment = CartPayment.Apply(
            cart, new ProvisionedUserWallet(account, wallet), Account.CreateSystemAccount());
        return new PaidMixedCart(cart, wallet, payment);
    }

    private sealed record PaidMixedCart(Cart Cart, Wallet Wallet, CartPaymentRecords Payment);
}
