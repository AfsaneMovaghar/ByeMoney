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
    private const string Paid = nameof(OrderFinancialStatus.Paid);
    private const string Product = nameof(ProductItemType.Product);

    [Fact]
    public void CourseOnlyOrder_IsSettleableAfterPayment()
    {
        var order = Order(Paid, 100m, CourseItem());

        OrderSettlementRule.IsSettleable(order).Should().BeTrue();
    }

    [Theory]
    [InlineData(nameof(OrderFinancialStatus.AwaitingPayment))]
    [InlineData(nameof(OrderFinancialStatus.Processing))]
    [InlineData(nameof(OrderFinancialStatus.RequiresReview))]
    [InlineData(nameof(OrderFinancialStatus.Failed))]
    public void UnpaidOrder_IsNotSettleable(string financialStatus)
    {
        var order = Order(financialStatus, 100m, CourseItem());

        OrderSettlementRule.IsSettleable(order).Should().BeFalse();
    }

    [Fact]
    public void PaidOrderWithoutFinalAmount_IsNotSettleable()
    {
        var order = Order(Paid, null, CourseItem());

        OrderSettlementRule.IsSettleable(order).Should().BeFalse();
    }

    [Fact]
    public void UnshippedPhysicalItem_BlocksSettlement()
    {
        var order = Order(Paid, 100m, CourseItem(), PhysicalItem(PhysicalDeliveryStatus.Preparing));

        OrderSettlementRule.IsSettleable(order).Should().BeFalse();
    }

    [Fact]
    public void PartiallyShippedOrder_IsNotSettleable()
    {
        var order = Order(Paid, 100m,
            PhysicalItem(PhysicalDeliveryStatus.Shipped, "book"),
            PhysicalItem(PhysicalDeliveryStatus.Preparing, "pen"));

        OrderSettlementRule.IsSettleable(order).Should().BeFalse();
    }

    [Theory]
    [InlineData(PhysicalDeliveryStatus.Shipped)]
    [InlineData(PhysicalDeliveryStatus.Delivered)]
    public void ShippedOrDeliveredPhysicalItems_AreSettleable(PhysicalDeliveryStatus status)
    {
        var order = Order(Paid, 100m, CourseItem(), PhysicalItem(status));

        OrderSettlementRule.IsSettleable(order).Should().BeTrue();
    }

    [Theory]
    [InlineData(PhysicalDeliveryStatus.Shipped)]
    [InlineData(PhysicalDeliveryStatus.Delivered)]
    public void CarrierReturnedRemainingPhysicalItem_IsNotSettleable(PhysicalDeliveryStatus status)
    {
        var order = Order(Paid, 100m, CourseItem(),
            PhysicalItem(status, carrierReturnedAtUtc: DateTime.UtcNow));

        OrderSettlementRule.IsSettleable(order).Should().BeFalse();
    }

    [Theory]
    [InlineData(PhysicalDeliveryStatus.Shipped)]
    [InlineData(PhysicalDeliveryStatus.Delivered)]
    public void UnresolvedCarrierAlert_IsNotSettleable_EvenAfterBuyerConfirmation(PhysicalDeliveryStatus status)
    {
        var order = Order(Paid, 100m, CourseItem(),
            PhysicalItem(status, carrierAlertCode: 11304));

        OrderSettlementRule.IsSettleable(order).Should().BeFalse();
    }

    [Fact]
    public void CarrierAlertClosedWithoutSettlement_IsNotSettleable()
    {
        var order = Order(Paid, 100m, CourseItem(),
            PhysicalItem(PhysicalDeliveryStatus.Shipped, carrierAlertCode: 11305,
                carrierAlertResolution: nameof(CarrierReturnResolution.ClosedWithoutSettlement)));

        OrderSettlementRule.IsSettleable(order).Should().BeFalse();
    }

    [Fact]
    public void CarrierAlertDeliveryConfirmed_IsSettleable()
    {
        var order = Order(Paid, 100m, CourseItem(),
            PhysicalItem(PhysicalDeliveryStatus.Delivered, carrierAlertCode: 11304,
                carrierAlertResolution: nameof(CarrierReturnResolution.DeliveryConfirmed)));

        OrderSettlementRule.IsSettleable(order).Should().BeTrue();
    }

    [Fact]
    public void BuyerConfirmationDoesNotClearCarrierAlert()
    {
        var buyer = new UserId(Guid.NewGuid());
        var purchase = PhysicalProductPurchase.Create(PhysicalProductPurchaseId.New(), Guid.NewGuid(), buyer, "book", "Book", 1, 10m, 10m, FinancialScope.TarheElahi);
        purchase.MarkShipped("TRACK-1");
        purchase.MarkCarrierAlert(11304, DateTime.UtcNow);

        purchase.MarkDelivered();

        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Delivered);
        purchase.HasUnresolvedCarrierAlert.Should().BeTrue();
    }

    [Fact]
    public void CarrierReturnedOnFullyCancelledPhysicalItem_DoesNotBlockSettlement()
    {
        var order = Order(Paid, 100m,
            CourseItem(),
            PhysicalItem(PhysicalDeliveryStatus.Preparing, quantity: 1, cancelledQuantity: 1,
                carrierReturnedAtUtc: DateTime.UtcNow));

        OrderSettlementRule.IsSettleable(order).Should().BeTrue();
    }

    [Fact]
    public void ItemCancelledBeforeShipping_IsIgnored()
    {
        var order = Order(Paid, 100m,
            PhysicalItem(PhysicalDeliveryStatus.Shipped, "book"),
            PhysicalItem(PhysicalDeliveryStatus.Preparing, "pen", quantity: 2, cancelledQuantity: 2));

        OrderSettlementRule.IsSettleable(order).Should().BeTrue();
    }

    [Fact]
    public void PartiallyCancelledQuantity_RequiresRemainderToBeShipped()
    {
        var waiting = Order(Paid, 100m,
            PhysicalItem(PhysicalDeliveryStatus.Preparing, quantity: 3, cancelledQuantity: 1));
        var shipped = Order(Paid, 100m,
            PhysicalItem(PhysicalDeliveryStatus.Shipped, quantity: 3, cancelledQuantity: 1));

        OrderSettlementRule.IsSettleable(waiting).Should().BeFalse();
        OrderSettlementRule.IsSettleable(shipped).Should().BeTrue();
    }

    [Fact]
    public void FullyCancelledPhysicalOrder_IsNotSettleable()
    {
        var order = Order(Paid, 100m,
            PhysicalItem(PhysicalDeliveryStatus.Preparing, quantity: 2, cancelledQuantity: 2));

        OrderSettlementRule.IsSettleable(order).Should().BeFalse();
    }

    [Fact]
    public void CourseWithFullyCancelledPhysicalItem_IsSettleable()
    {
        var order = Order(Paid, 100m,
            CourseItem(),
            PhysicalItem(PhysicalDeliveryStatus.Preparing, quantity: 1, cancelledQuantity: 1));

        OrderSettlementRule.IsSettleable(order).Should().BeTrue();
    }

    [Fact]
    public void NetAmount_SubtractsRefund()
    {
        var order = Order(Paid, 110m, CourseItem());

        OrderSettlementRule.NetAmount(order, 30m).Should().Be(80m);
        OrderSettlementRule.NetAmount(order, 0m).Should().Be(110m);
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
        var openOrderId = fixture.AddPaidCoursePurchase("course-b", 20m, giftShare: 5m);
        fixture.SettledOrderIds.Add(settledOrderId);

        var result = await fixture.CloseAsync(await fixture.PreviewTokenAsync());

        result.IsSuccess.Should().BeTrue();
        fixture.SavedPeriod!.Items.Select(item => item.OrderId).Should().Equal(openOrderId);
        fixture.SavedPeriod.OrderCount.Should().Be(1);
        fixture.SavedPeriod.TotalRegularNetAmountInNoor.Should().Be(15m);
        fixture.SavedPeriod.TotalGiftNetAmountInNoor.Should().Be(5m);
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
    public async Task Candidates_SplitNetAmountIntoRegularAndGiftAfterRefund()
    {
        // سبد: کتاب ۲×۵۰ (۸۰ عادی، ۲۰ هدیه؛ ارسال‌شده)، خودکار ۱×۲۰ (۱۵ عادی، ۵ هدیه؛ پیش از ارسال لغو و کامل برگشت شد)،
        // ارسال ۱۰ نور عادی از Ledger سبد.
        var fixture = new HandlerFixture();
        var cartOrderId = fixture.AddPaidPhysicalCartWithCancelledPen();

        var orders = await fixture.CandidatesAsync();

        var order = orders.Single();
        order.OrderId.Should().Be(cartOrderId);
        order.RegularNetAmountInNoor.Should().Be(90m);
        order.GiftNetAmountInNoor.Should().Be(20m);
        order.NetAmountInNoor.Should().Be(110m);
    }

    [Fact]
    public async Task Candidates_ExcludeOrderWithoutRecordedPaymentShares()
    {
        var fixture = new HandlerFixture();
        fixture.AddPaidCoursePurchase("legacy-course", 30m, recordShares: false);

        var orders = await fixture.CandidatesAsync();

        orders.Should().BeEmpty();
    }

    [Fact]
    public async Task Candidates_ExcludeOrderWithCarrierReturnedRemainingPhysicalItem()
    {
        var fixture = new HandlerFixture();
        fixture.AddPaidShippedPhysicalCart(carrierReturned: true);

        var orders = await fixture.CandidatesAsync();

        orders.Should().BeEmpty();
    }

    [Fact]
    public async Task Candidates_IncludeShippedPhysicalOrderWithoutCarrierReturn()
    {
        var fixture = new HandlerFixture();
        var orderId = fixture.AddPaidShippedPhysicalCart(carrierReturned: false);

        var orders = await fixture.CandidatesAsync();

        orders.Should().ContainSingle().Which.OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task Candidates_IncludeOrderWhenCarrierReturnResolvedWithDeliveryConfirmed()
    {
        var fixture = new HandlerFixture();
        var orderId = fixture.AddPaidShippedPhysicalCart(carrierReturned: true, CarrierReturnResolution.DeliveryConfirmed);

        var orders = await fixture.CandidatesAsync();

        orders.Should().ContainSingle().Which.OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task Candidates_PermanentlyExcludeOrderWhenCarrierReturnResolvedWithClosedWithoutSettlement()
    {
        var fixture = new HandlerFixture();
        fixture.AddPaidShippedPhysicalCart(carrierReturned: true, CarrierReturnResolution.ClosedWithoutSettlement);

        var orders = await fixture.CandidatesAsync();

        orders.Should().BeEmpty();
    }

    [Fact]
    public void ClosePeriod_RejectsEmptyAndDuplicateOrders()
    {
        var admin = new UserId(Guid.NewGuid());
        var shares = new PaymentShares(10m, 0m);
        var item = OrderSettlementItem.Create("purchase:" + Guid.NewGuid(), "Purchase", "buyer", DateTime.UtcNow, shares);
        var duplicate = OrderSettlementItem.Create(item.OrderId, "Purchase", "buyer", DateTime.UtcNow, shares);

        var empty = () => OrderSettlementPeriod.Close(admin, null, null, [], FinancialScope.TarheElahi);
        var repeated = () => OrderSettlementPeriod.Close(admin, null, null, [item, duplicate], FinancialScope.TarheElahi);

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
        var x = new SettlementOrderResponse("purchase:x", "Purchase", "buyer", DateTime.UtcNow, 30m, 10m, 40m);
        var y = x with { OrderId = "purchase:y" };
        var resplit = x with { RegularNetAmountInNoor = 35m, GiftNetAmountInNoor = 5m };

        OrderSettlementPreviewToken.Compute(FinancialScope.TarheElahi, [x]).Should().HaveLength(OrderSettlementPreviewToken.Length);
        OrderSettlementPreviewToken.Compute(FinancialScope.TarheElahi, [x]).Should().Be(OrderSettlementPreviewToken.Compute(FinancialScope.TarheElahi, [x with { }]));
        OrderSettlementPreviewToken.Compute(FinancialScope.TarheElahi, [x]).Should().NotBe(OrderSettlementPreviewToken.Compute(FinancialScope.TarheElahi, [y]));
        OrderSettlementPreviewToken.Compute(FinancialScope.TarheElahi, [x]).Should().NotBe(OrderSettlementPreviewToken.Compute(FinancialScope.TarheElahi, [resplit]));
    }

    private static OrderDetails Order(string financialStatus, decimal? finalAmount, params OrderItem[] items) =>
        new(
            "cart:" + Guid.NewGuid(), nameof(OrderSourceType.Cart), Guid.NewGuid(),
            DateTime.UtcNow, DateTime.UtcNow, financialStatus, nameof(OrderDeliveryStatus.Pending),
            finalAmount, items.Length, null, null, null, null, items, [], null);

    private static OrderItem CourseItem() =>
        new("course", "Course", 100m, 100m, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow,
            Paid, nameof(OrderDeliveryStatus.Pending));

    private static OrderItem PhysicalItem(
        PhysicalDeliveryStatus status,
        string externalId = "book",
        int quantity = 1,
        int cancelledQuantity = 0,
        DateTime? carrierReturnedAtUtc = null,
        int? carrierAlertCode = null,
        string? carrierAlertResolution = null) =>
        new(externalId, externalId, 10m, 10m, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow,
            Paid, nameof(OrderDeliveryStatus.Pending), Product, null, quantity, status.ToString(),
            cancelledQuantity, CarrierReturnedAtUtc: carrierReturnedAtUtc,
            CarrierAlertCode: carrierAlertCode, CarrierAlertResolution: carrierAlertResolution);

    private sealed class HandlerFixture
    {
        private readonly List<CoursePurchase> _purchases = [];
        private readonly List<Cart> _carts = [];
        private readonly List<PhysicalProductPurchase> _physicalPurchases = [];
        private readonly List<PhysicalProductCancellation> _cancellations = [];
        private readonly OrderBuyer _buyer = new(Guid.NewGuid(), "buyer-document", "Buyer");
        private readonly OrderSettlementCandidateService _candidates;

        public HandlerFixture()
        {
            var reader = new Mock<IOrderReader>();
            reader
                .Setup(x => x.ReadAsync(null, null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new OrderSnapshot(
                    _carts, [], _purchases, [_buyer], _physicalPurchases, _cancellations));

            Store
                .Setup(x => x.GetSettledOrderIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => SettledOrderIds);
            Store
                .Setup(x => x.GetShippingPaymentsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => ShippingByCart);
            Store
                .Setup(x => x.TryAddAsync(It.IsAny<OrderSettlementPeriod>(), It.IsAny<CancellationToken>()))
                .Callback<OrderSettlementPeriod, CancellationToken>((period, _) => SavedPeriod = period)
                .ReturnsAsync(() => AcceptSave);
            Store
                .Setup(x => x.FindPeriodAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, string _, CancellationToken __) => new OrderSettlementPeriodResponse(id, SavedPeriod!.Title, SavedPeriod.Note, SavedPeriod.OrderCount, SavedPeriod.TotalRegularNetAmountInNoor, SavedPeriod.TotalGiftNetAmountInNoor, SavedPeriod.TotalNetAmountInNoor, "admin-document", SavedPeriod.CreatedAtUtc, FinancialScope.TarheElahi));

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(x => x.UserId).Returns(AdminId);

            _candidates = new OrderSettlementCandidateService(reader.Object, Store.Object);
            Handler = new CloseOrderSettlementPeriodCommandHandler(_candidates, Store.Object, currentUser.Object);
        }

        public Guid AdminId { get; } = Guid.NewGuid();
        public Mock<IOrderSettlementStore> Store { get; } = new();
        public HashSet<string> SettledOrderIds { get; } = new(StringComparer.Ordinal);
        public Dictionary<Guid, PaymentShares> ShippingByCart { get; } = [];
        public bool AcceptSave { get; init; } = true;
        public OrderSettlementPeriod? SavedPeriod { get; private set; }
        private CloseOrderSettlementPeriodCommandHandler Handler { get; }

        public string AddPaidCoursePurchase(
            string externalId, decimal price, decimal giftShare = 0m, bool recordShares = true)
        {
            var purchase = CoursePurchase.Create(new UserId(_buyer.Id), ProductSnapshot.Create(externalId, externalId, price), FinancialScope.TarheElahi);
            purchase.MarkDebited(Guid.NewGuid());
            if (recordShares)
            {
                purchase.RecordPaymentShares(new PaymentShares(price - giftShare, giftShare));
            }

            _purchases.Add(purchase);
            return "purchase:" + purchase.Id.Value.ToString("D");
        }

        public string AddPaidPhysicalCartWithCancelledPen()
        {
            var buyerId = new UserId(_buyer.Id);
            var cart = Cart.Create(Guid.NewGuid(), _buyer.Id, _buyer.ExternalUserId, [
                new CartItemInput("book", ProductItemType.Product, 2),
                new CartItemInput("pen", ProductItemType.Product, 1)
            ], null, FinancialScope.TarheElahi);
            cart.SetQuote([
                new CartQuoteItem("book", "Book", 50, 0, 0, 100, ProductItemType.Product, null, 2),
                new CartQuoteItem("pen", "Pen", 20, 0, 0, 20, ProductItemType.Product, null, 1)
            ], shippingAmountInNoor: 10);
            cart.MarkPaid();

            var book = PhysicalProductPurchase.Create(PhysicalProductPurchaseId.New(), cart.Id, buyerId, "book", "Book", 2, 50, 100, FinancialScope.TarheElahi);
            book.MarkDebited(cart.Id);
            book.RecordPaymentShares(new PaymentShares(80m, 20m));
            book.MarkShipped("TRACK-1");

            var pen = PhysicalProductPurchase.Create(PhysicalProductPurchaseId.New(), cart.Id, buyerId, "pen", "Pen", 1, 20, 20, FinancialScope.TarheElahi);
            pen.MarkDebited(cart.Id);
            pen.RecordPaymentShares(new PaymentShares(15m, 5m));
            var refund = new PaymentShares(15m, 5m);
            pen.RecordCancellation(1, refund);
            var cancellation = PhysicalProductCancellation.Create(
                Guid.NewGuid(), pen, 1, refund, new PaymentShares(0m, 0m),
                PhysicalCancellationActor.Admin, new UserId(AdminId), "لغو پیش از ارسال");

            _carts.Add(cart);
            ShippingByCart[cart.Id] = new PaymentShares(10m, 0m);
            _physicalPurchases.AddRange([book, pen]);
            _cancellations.Add(cancellation);
            return "cart:" + cart.Id.ToString("D");
        }

        public string AddPaidShippedPhysicalCart(bool carrierReturned, CarrierReturnResolution? resolution = null)
        {
            var buyerId = new UserId(_buyer.Id);
            var cart = Cart.Create(Guid.NewGuid(), _buyer.Id, _buyer.ExternalUserId, [
                new CartItemInput("book", ProductItemType.Product, 1)
            ], null, FinancialScope.TarheElahi);
            cart.SetQuote([
                new CartQuoteItem("book", "Book", 50, 0, 0, 50, ProductItemType.Product, null, 1)
            ], shippingAmountInNoor: 10);
            cart.MarkPaid();

            var book = PhysicalProductPurchase.Create(PhysicalProductPurchaseId.New(), cart.Id, buyerId, "book", "Book", 1, 50, 50, FinancialScope.TarheElahi);
            book.MarkDebited(cart.Id);
            book.RecordPaymentShares(new PaymentShares(50m, 0m));
            book.MarkShipped("TRACK-1");
            if (carrierReturned)
            {
                book.MarkCarrierReturned(DateTime.UtcNow);
                if (resolution.HasValue)
                {
                    book.ResolveCarrierReturn(resolution.Value, "Resolution note", Guid.NewGuid(), DateTime.UtcNow);
                }
            }

            _carts.Add(cart);
            ShippingByCart[cart.Id] = new PaymentShares(10m, 0m);
            _physicalPurchases.Add(book);
            return "cart:" + cart.Id.ToString("D");
        }

        public Task<IReadOnlyList<SettlementOrderResponse>> CandidatesAsync() =>
            _candidates.GetUnsettledAsync(FinancialScope.TarheElahi, default);

        public async Task<string> PreviewTokenAsync() =>
            OrderSettlementPreviewToken.Compute(FinancialScope.TarheElahi, await CandidatesAsync());

        public Task<ByeMoney.Domain.Common.Generic.Result<OrderSettlementPeriodResponse>> CloseAsync(string previewToken) =>
            Handler.Handle(new CloseOrderSettlementPeriodCommand("دوره", null, previewToken), default);
    }
}
