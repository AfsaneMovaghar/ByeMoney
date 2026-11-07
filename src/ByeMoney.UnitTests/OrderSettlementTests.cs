using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Commands.CloseOrderSettlementPeriod;
using ByeMoney.Application.Modules.Purchases.Orders.Constants;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Settlements.Constants;
using ByeMoney.Application.Modules.Purchases.Settlements.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.Interfaces;
using ByeMoney.Application.Modules.Purchases.Settlements.Services;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using FluentAssertions;
using Moq;

namespace ByeMoney.UnitTests;

public sealed class OrderSettlementTests
{
    private const string Paid = nameof(CourseOrderFinancialStatus.Paid);
    private const string Product = nameof(ProductItemType.Product);

    [Fact]
    public void CourseOnlyOrder_IsSettleableAfterPayment()
    {
        var order = Order(Paid, 100m, CourseItem());

        CourseOrderSettlementRule.IsSettleable(order).Should().BeTrue();
    }

    [Theory]
    [InlineData(nameof(CourseOrderFinancialStatus.AwaitingPayment))]
    [InlineData(nameof(CourseOrderFinancialStatus.Processing))]
    [InlineData(nameof(CourseOrderFinancialStatus.RequiresReview))]
    [InlineData(nameof(CourseOrderFinancialStatus.Failed))]
    public void UnpaidOrder_IsNotSettleable(string financialStatus)
    {
        var order = Order(financialStatus, 100m, CourseItem());

        CourseOrderSettlementRule.IsSettleable(order).Should().BeFalse();
    }

    [Fact]
    public void PaidOrderWithoutFinalAmount_IsNotSettleable()
    {
        var order = Order(Paid, null, CourseItem());

        CourseOrderSettlementRule.IsSettleable(order).Should().BeFalse();
    }

    [Fact]
    public void UnshippedPhysicalItem_BlocksSettlement()
    {
        var order = Order(Paid, 100m, CourseItem(), PhysicalItem(PhysicalDeliveryStatus.Preparing));

        CourseOrderSettlementRule.IsSettleable(order).Should().BeFalse();
    }

    [Fact]
    public void PartiallyShippedOrder_IsNotSettleable()
    {
        var order = Order(Paid, 100m,
            PhysicalItem(PhysicalDeliveryStatus.Shipped, "book"),
            PhysicalItem(PhysicalDeliveryStatus.Preparing, "pen"));

        CourseOrderSettlementRule.IsSettleable(order).Should().BeFalse();
    }

    [Theory]
    [InlineData(PhysicalDeliveryStatus.Shipped)]
    [InlineData(PhysicalDeliveryStatus.Delivered)]
    public void ShippedOrDeliveredPhysicalItems_AreSettleable(PhysicalDeliveryStatus status)
    {
        var order = Order(Paid, 100m, CourseItem(), PhysicalItem(status));

        CourseOrderSettlementRule.IsSettleable(order).Should().BeTrue();
    }

    [Fact]
    public void ItemCancelledBeforeShipping_IsIgnored()
    {
        var order = Order(Paid, 100m,
            PhysicalItem(PhysicalDeliveryStatus.Shipped, "book"),
            PhysicalItem(PhysicalDeliveryStatus.Preparing, "pen", quantity: 2, cancelledQuantity: 2));

        CourseOrderSettlementRule.IsSettleable(order).Should().BeTrue();
    }

    [Fact]
    public void PartiallyCancelledQuantity_RequiresRemainderToBeShipped()
    {
        var waiting = Order(Paid, 100m,
            PhysicalItem(PhysicalDeliveryStatus.Preparing, quantity: 3, cancelledQuantity: 1));
        var shipped = Order(Paid, 100m,
            PhysicalItem(PhysicalDeliveryStatus.Shipped, quantity: 3, cancelledQuantity: 1));

        CourseOrderSettlementRule.IsSettleable(waiting).Should().BeFalse();
        CourseOrderSettlementRule.IsSettleable(shipped).Should().BeTrue();
    }

    [Fact]
    public void FullyCancelledPhysicalOrder_IsNotSettleable()
    {
        var order = Order(Paid, 100m,
            PhysicalItem(PhysicalDeliveryStatus.Preparing, quantity: 2, cancelledQuantity: 2));

        CourseOrderSettlementRule.IsSettleable(order).Should().BeFalse();
    }

    [Fact]
    public void CourseWithFullyCancelledPhysicalItem_IsSettleable()
    {
        var order = Order(Paid, 100m,
            CourseItem(),
            PhysicalItem(PhysicalDeliveryStatus.Preparing, quantity: 1, cancelledQuantity: 1));

        CourseOrderSettlementRule.IsSettleable(order).Should().BeTrue();
    }

    [Fact]
    public void NetAmount_SubtractsRefund()
    {
        var order = Order(Paid, 110m, CourseItem());

        CourseOrderSettlementRule.NetAmount(order, 30m).Should().Be(80m);
        CourseOrderSettlementRule.NetAmount(order, 0m).Should().Be(110m);
    }

    [Fact]
    public async Task Close_WithNoSettleableOrder_ReturnsEmptyConflict()
    {
        var fixture = new HandlerFixture();

        var result = await fixture.CloseAsync(new string('a', OrderSettlementPreviewToken.Length));

        result.Status.Should().Be(ResultStatus.Conflict);
        result.ErrorCode.Should().Be(OrderSettlementErrorCodes.Empty);
        fixture.Store.Verify(x => x.TryAddAsync(It.IsAny<OrderSettlementPeriod>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Close_WhenSettledOrderIsReplacedBySameAmountOrder_ReturnsPreviewChanged()
    {
        // ادمین X=40 را دید؛ پیش از بستن، ادمین دیگر X را بست و Y=40 قابل تسویه شد.
        // تعداد و جمع یکی است، ولی فهرست همانی نیست که ادمین تأیید کرده است.
        var fixture = new HandlerFixture();
        var seenOrderId = fixture.AddPaidCoursePurchase("course-x", 40m);
        var seenToken = await fixture.PreviewTokenAsync();
        fixture.SettledOrderIds.Add(seenOrderId);
        fixture.AddPaidCoursePurchase("course-y", 40m);

        var result = await fixture.CloseAsync(seenToken);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.ErrorCode.Should().Be(OrderSettlementErrorCodes.PreviewChanged);
        fixture.Store.Verify(x => x.TryAddAsync(It.IsAny<OrderSettlementPeriod>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Close_WhenNewOrderBecameSettleable_ReturnsPreviewChanged()
    {
        var fixture = new HandlerFixture();
        fixture.AddPaidCoursePurchase("course-a", 30m);
        var seenToken = await fixture.PreviewTokenAsync();
        fixture.AddPaidCoursePurchase("course-b", 20m);

        var result = await fixture.CloseAsync(seenToken);

        result.ErrorCode.Should().Be(OrderSettlementErrorCodes.PreviewChanged);
    }

    [Fact]
    public async Task Close_ExcludesAlreadySettledOrders()
    {
        var fixture = new HandlerFixture();
        var settledOrderId = fixture.AddPaidCoursePurchase("course-a", 30m);
        var openOrderId = fixture.AddPaidCoursePurchase("course-b", 20m);
        fixture.SettledOrderIds.Add(settledOrderId);

        var result = await fixture.CloseAsync(await fixture.PreviewTokenAsync());

        result.IsSuccess.Should().BeTrue();
        fixture.SavedPeriod!.Items.Select(item => item.OrderId).Should().Equal(openOrderId);
        fixture.SavedPeriod.OrderCount.Should().Be(1);
        fixture.SavedPeriod.TotalNetAmountInNoor.Should().Be(20m);
        fixture.SavedPeriod.ClosedByUserId.Should().Be(new UserId(fixture.AdminId));
    }

    [Fact]
    public async Task Close_WhenDatabaseRejectsDuplicate_ReturnsAlreadySettledConflict()
    {
        var fixture = new HandlerFixture { AcceptSave = false };
        fixture.AddPaidCoursePurchase("course-a", 30m);

        var result = await fixture.CloseAsync(await fixture.PreviewTokenAsync());

        result.Status.Should().Be(ResultStatus.Conflict);
        result.ErrorCode.Should().Be(OrderSettlementErrorCodes.AlreadySettled);
    }

    [Fact]
    public async Task Candidates_NetAmountSubtractsRealCancellationRefund()
    {
        // سبد: کتاب ۲×۵۰ (ارسال‌شده)، خودکار ۱×۲۰ (پیش از ارسال لغو و ۲۰ نور برگشت شد)، ارسال ۱۰.
        var fixture = new HandlerFixture();
        var cartOrderId = fixture.AddPaidPhysicalCartWithCancelledPen();

        var orders = await fixture.CandidatesAsync();

        var order = orders.Single();
        order.OrderId.Should().Be(cartOrderId);
        order.NetAmountInNoor.Should().Be(110m);
    }

    [Fact]
    public void ClosePeriod_RejectsEmptyAndDuplicateOrders()
    {
        var admin = new UserId(Guid.NewGuid());
        var item = OrderSettlementItem.Create("purchase:" + Guid.NewGuid(), "Purchase", "buyer", DateTime.UtcNow, 10m);
        var duplicate = OrderSettlementItem.Create(item.OrderId, "Purchase", "buyer", DateTime.UtcNow, 10m);

        var empty = () => OrderSettlementPeriod.Close(admin, null, null, []);
        var repeated = () => OrderSettlementPeriod.Close(admin, null, null, [item, duplicate]);

        empty.Should().Throw<ArgumentException>();
        repeated.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloseValidator_RejectsInvalidInput()
    {
        var validator = new CloseOrderSettlementPeriodCommandValidator();

        var token = new string('a', OrderSettlementPreviewToken.Length);

        validator.Validate(new CloseOrderSettlementPeriodCommand(null, null, "")).IsValid.Should().BeFalse();
        validator.Validate(new CloseOrderSettlementPeriodCommand(null, null, "short")).IsValid.Should().BeFalse();
        validator.Validate(new CloseOrderSettlementPeriodCommand(new string('x', 201), null, token)).IsValid.Should().BeFalse();
        validator.Validate(new CloseOrderSettlementPeriodCommand(null, new string('x', 1001), token)).IsValid.Should().BeFalse();
        validator.Validate(new CloseOrderSettlementPeriodCommand("آبان", "یادداشت", token)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void PreviewToken_DependsOnOrderIdsAndAmounts()
    {
        var x = new SettlementOrderResponse("purchase:x", "Purchase", "buyer", DateTime.UtcNow, 40m);
        var y = x with { OrderId = "purchase:y" };

        OrderSettlementPreviewToken.Compute([x]).Should().HaveLength(OrderSettlementPreviewToken.Length);
        OrderSettlementPreviewToken.Compute([x]).Should().Be(OrderSettlementPreviewToken.Compute([x with { }]));
        OrderSettlementPreviewToken.Compute([x]).Should().NotBe(OrderSettlementPreviewToken.Compute([y]));
        OrderSettlementPreviewToken.Compute([x]).Should().NotBe(
            OrderSettlementPreviewToken.Compute([x with { NetAmountInNoor = 41m }]));
    }

    private static CourseOrderDetails Order(string financialStatus, decimal? finalAmount, params CourseOrderItem[] items) =>
        new(
            "cart:" + Guid.NewGuid(), nameof(CourseOrderSourceType.Cart), Guid.NewGuid(),
            DateTime.UtcNow, DateTime.UtcNow, financialStatus, nameof(CourseOrderDeliveryStatus.Pending),
            finalAmount, items.Length, null, null, null, null, items, []);

    private static CourseOrderItem CourseItem() =>
        new("course", "Course", 100m, 100m, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow,
            Paid, nameof(CourseOrderDeliveryStatus.Pending));

    private static CourseOrderItem PhysicalItem(
        PhysicalDeliveryStatus status, string externalId = "book", int quantity = 1, int cancelledQuantity = 0) =>
        new(externalId, externalId, 10m, 10m, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow,
            Paid, nameof(CourseOrderDeliveryStatus.Pending), Product, null, quantity, status.ToString(),
            cancelledQuantity);

    private sealed class HandlerFixture
    {
        private readonly List<CoursePurchase> _purchases = [];
        private readonly List<Cart> _carts = [];
        private readonly List<PhysicalProductPurchase> _physicalPurchases = [];
        private readonly List<PhysicalProductCancellation> _cancellations = [];
        private readonly CourseOrderBuyer _buyer = new(Guid.NewGuid(), "buyer-document", "Buyer");
        private readonly OrderSettlementCandidateService _candidates;

        public HandlerFixture()
        {
            var reader = new Mock<ICourseOrderReader>();
            reader
                .Setup(x => x.ReadAsync(null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new CourseOrderSnapshot(
                    _carts, [], _purchases, [_buyer], _physicalPurchases, _cancellations));

            Store
                .Setup(x => x.GetSettledOrderIdsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => SettledOrderIds);
            Store
                .Setup(x => x.TryAddAsync(It.IsAny<OrderSettlementPeriod>(), It.IsAny<CancellationToken>()))
                .Callback<OrderSettlementPeriod, CancellationToken>((period, _) => SavedPeriod = period)
                .ReturnsAsync(() => AcceptSave);
            Store
                .Setup(x => x.FindPeriodAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => new OrderSettlementPeriodResponse(
                    id, SavedPeriod!.Title, SavedPeriod.Note, SavedPeriod.OrderCount,
                    SavedPeriod.TotalNetAmountInNoor, "admin-document", SavedPeriod.CreatedAtUtc));

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(x => x.UserId).Returns(AdminId);

            _candidates = new OrderSettlementCandidateService(reader.Object, Store.Object);
            Handler = new CloseOrderSettlementPeriodCommandHandler(_candidates, Store.Object, currentUser.Object);
        }

        public Guid AdminId { get; } = Guid.NewGuid();
        public Mock<IOrderSettlementStore> Store { get; } = new();
        public HashSet<string> SettledOrderIds { get; } = new(StringComparer.Ordinal);
        public bool AcceptSave { get; init; } = true;
        public OrderSettlementPeriod? SavedPeriod { get; private set; }
        private CloseOrderSettlementPeriodCommandHandler Handler { get; }

        public string AddPaidCoursePurchase(string externalId, decimal price)
        {
            var purchase = CoursePurchase.Create(new UserId(_buyer.Id), ProductSnapshot.Create(externalId, externalId, price));
            purchase.MarkDebited(Guid.NewGuid());
            _purchases.Add(purchase);
            return "purchase:" + purchase.Id.Value.ToString("D");
        }

        public string AddPaidPhysicalCartWithCancelledPen()
        {
            var buyerId = new UserId(_buyer.Id);
            var cart = Cart.Create(Guid.NewGuid(), _buyer.Id, _buyer.ExternalUserId, [
                new CartItemInput("book", ProductItemType.Product, 2),
                new CartItemInput("pen", ProductItemType.Product, 1)
            ], null);
            cart.SetQuote([
                new CartQuoteItem("book", "Book", 50, 0, 0, 100, ProductItemType.Product, null, 2),
                new CartQuoteItem("pen", "Pen", 20, 0, 0, 20, ProductItemType.Product, null, 1)
            ], shippingAmountInNoor: 10);
            cart.MarkPaid();

            var book = PhysicalProductPurchase.Create(
                PhysicalProductPurchaseId.New(), cart.Id, buyerId, "book", "Book", 2, 50, 100);
            book.MarkDebited(cart.Id);
            book.MarkShipped();

            var pen = PhysicalProductPurchase.Create(
                PhysicalProductPurchaseId.New(), cart.Id, buyerId, "pen", "Pen", 1, 20, 20);
            pen.MarkDebited(cart.Id);
            pen.RecordPaymentShares(new PaymentShares(15m, 5m));
            var refund = new PaymentShares(15m, 5m);
            pen.RecordCancellation(1, refund);
            var cancellation = PhysicalProductCancellation.Create(
                Guid.NewGuid(), pen, 1, refund, new PaymentShares(0m, 0m),
                PhysicalCancellationActor.Admin, new UserId(AdminId), "لغو پیش از ارسال");

            _carts.Add(cart);
            _physicalPurchases.AddRange([book, pen]);
            _cancellations.Add(cancellation);
            return "cart:" + cart.Id.ToString("D");
        }

        public Task<IReadOnlyList<SettlementOrderResponse>> CandidatesAsync() =>
            _candidates.GetUnsettledAsync(default);

        public async Task<string> PreviewTokenAsync() =>
            OrderSettlementPreviewToken.Compute(await CandidatesAsync());

        public Task<ByeMoney.Domain.Common.Generic.Result<OrderSettlementPeriodResponse>> CloseAsync(string previewToken) =>
            Handler.Handle(new CloseOrderSettlementPeriodCommand("دوره", null, previewToken), default);
    }
}
