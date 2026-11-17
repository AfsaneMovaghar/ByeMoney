using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Commands.ConfirmMyPhysicalDelivery;
using ByeMoney.Application.Modules.Purchases.Commands.ResolveCarrierReturn;
using ByeMoney.Application.Modules.Purchases.Commands.UpdatePhysicalDeliveryStatus;
using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Constants;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminOrder;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminOrders;
using ByeMoney.Application.Modules.Purchases.Queries.GetMyOrder;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using FluentAssertions;
using Moq;

namespace ByeMoney.UnitTests;

public sealed class CarrierReturnResolutionTests
{
    private static readonly Guid BuyerId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid CartId = Guid.NewGuid();

    [Fact]
    public void ResolveCarrierReturn_RequiresCarrierReturnedAtUtc()
    {
        var purchase = CreateShippedPurchase();

        var act = () => purchase.ResolveCarrierReturn(
            CarrierReturnResolution.DeliveryConfirmed, "Note", AdminId, DateTime.UtcNow);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ResolveCarrierReturn_WithDeliveryConfirmed_MarksDeliveredAndSetsMetadata()
    {
        var purchase = CreateShippedPurchase();
        var returnedAt = DateTime.UtcNow.AddHours(-2);
        var resolvedAt = DateTime.UtcNow;
        purchase.MarkCarrierReturned(returnedAt);

        purchase.ResolveCarrierReturn(CarrierReturnResolution.DeliveryConfirmed, "Customer received package", AdminId, resolvedAt);

        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Delivered);
        purchase.DeliveredAtUtc.Should().Be(resolvedAt);
        purchase.CarrierReturnedAtUtc.Should().Be(returnedAt);
        purchase.CarrierReturnResolution.Should().Be(CarrierReturnResolution.DeliveryConfirmed);
        purchase.CarrierReturnResolutionNote.Should().Be("Customer received package");
        purchase.CarrierReturnResolvedAtUtc.Should().Be(resolvedAt);
        purchase.CarrierReturnResolvedByUserId.Should().Be(AdminId);
        purchase.HasUnresolvedCarrierReturn.Should().BeFalse();
        purchase.HasClosedWithoutSettlementReturn.Should().BeFalse();
    }

    [Fact]
    public void ResolveCarrierReturn_WithClosedWithoutSettlement_KeepsShippedAndSetsMetadata()
    {
        var purchase = CreateShippedPurchase();
        var returnedAt = DateTime.UtcNow.AddHours(-2);
        var resolvedAt = DateTime.UtcNow;
        purchase.MarkCarrierReturned(returnedAt);

        purchase.ResolveCarrierReturn(CarrierReturnResolution.ClosedWithoutSettlement, "Carrier lost package; refund offline", AdminId, resolvedAt);

        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        purchase.DeliveredAtUtc.Should().BeNull();
        purchase.CarrierReturnedAtUtc.Should().Be(returnedAt);
        purchase.CarrierReturnResolution.Should().Be(CarrierReturnResolution.ClosedWithoutSettlement);
        purchase.CarrierReturnResolutionNote.Should().Be("Carrier lost package; refund offline");
        purchase.CarrierReturnResolvedAtUtc.Should().Be(resolvedAt);
        purchase.CarrierReturnResolvedByUserId.Should().Be(AdminId);
        purchase.HasUnresolvedCarrierReturn.Should().BeFalse();
        purchase.HasClosedWithoutSettlementReturn.Should().BeTrue();
    }

    [Fact]
    public void ResolveCarrierReturn_IsIdempotentOnExactRepeat_AndConflictsOnMismatchedRepeat()
    {
        var purchase = CreateShippedPurchase();
        purchase.MarkCarrierReturned(DateTime.UtcNow.AddHours(-1));
        var initialNow = DateTime.UtcNow;

        purchase.ResolveCarrierReturn(CarrierReturnResolution.ClosedWithoutSettlement, "Note A", AdminId, initialNow);

        // Exact repeat is no-op
        purchase.ResolveCarrierReturn(CarrierReturnResolution.ClosedWithoutSettlement, "Note A", AdminId, DateTime.UtcNow);
        purchase.CarrierReturnResolution.Should().Be(CarrierReturnResolution.ClosedWithoutSettlement);
        purchase.CarrierReturnResolutionNote.Should().Be("Note A");
        purchase.CarrierReturnResolvedAtUtc.Should().Be(initialNow);

        // Mismatched resolution conflicts (409)
        var conflictRes = () => purchase.ResolveCarrierReturn(
            CarrierReturnResolution.DeliveryConfirmed, "Note A", AdminId, DateTime.UtcNow);
        conflictRes.Should().Throw<ConflictDomainException>();

        // Mismatched note conflicts (409)
        var conflictNote = () => purchase.ResolveCarrierReturn(
            CarrierReturnResolution.ClosedWithoutSettlement, "Note B", AdminId, DateTime.UtcNow);
        conflictNote.Should().Throw<ConflictDomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveCarrierReturn_RejectsEmptyNote(string note)
    {
        var purchase = CreateShippedPurchase();
        purchase.MarkCarrierReturned(DateTime.UtcNow);

        var act = () => purchase.ResolveCarrierReturn(
            CarrierReturnResolution.DeliveryConfirmed, note, AdminId, DateTime.UtcNow);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkDelivered_BlockedByUnresolvedCarrierReturn()
    {
        var purchase = CreateShippedPurchase();
        purchase.MarkCarrierReturned(DateTime.UtcNow);

        var act = () => purchase.MarkDelivered();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkDelivered_BlockedByClosedWithoutSettlementReturn()
    {
        var purchase = CreateShippedPurchase();
        purchase.MarkCarrierReturned(DateTime.UtcNow);
        purchase.ResolveCarrierReturn(CarrierReturnResolution.ClosedWithoutSettlement, "Closed", AdminId, DateTime.UtcNow);

        var act = () => purchase.MarkDelivered();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public async Task AdminUpdateDeliveryStatus_BlocksDeliveredWhenCarrierReturnUnresolvedOrClosedWithoutSettlement()
    {
        var purchase = CreateShippedPurchase();
        purchase.MarkCarrierReturned(DateTime.UtcNow);

        var repo = new Mock<IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId>>();
        repo.Setup(r => r.GetByIdAsync(purchase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchase);

        var store = new Mock<IPhysicalDeliveryStore>();
        store.Setup(s => s.FindOwnerAsync(purchase.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhysicalPurchaseOwner(BuyerId, CartId));
        store.Setup(s => s.ExecuteLockedAsync(BuyerId, CartId, It.IsAny<Func<Task<PhysicalDeliveryStatusResponse>>>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, Guid, Func<Task<PhysicalDeliveryStatusResponse>>, CancellationToken>((_, _, work, _) => work());

        var unitOfWork = new Mock<IUnitOfWork>();

        var handler = new UpdatePhysicalDeliveryStatusCommandHandler(repo.Object, store.Object, unitOfWork.Object);

        // Target Delivered on unresolved return throws DomainException (HTTP 400)
        var actUnresolved = () => handler.Handle(
            new UpdatePhysicalDeliveryStatusCommand(purchase.Id.Value, nameof(PhysicalDeliveryStatus.Delivered)), default);
        await actUnresolved.Should().ThrowAsync<DomainException>();

        // Resolve as ClosedWithoutSettlement
        purchase.ResolveCarrierReturn(CarrierReturnResolution.ClosedWithoutSettlement, "Note", AdminId, DateTime.UtcNow);

        // Target Delivered on ClosedWithoutSettlement throws DomainException (HTTP 400)
        var actClosed = () => handler.Handle(
            new UpdatePhysicalDeliveryStatusCommand(purchase.Id.Value, nameof(PhysicalDeliveryStatus.Delivered)), default);
        await actClosed.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task BuyerConfirmDelivery_BlocksWhenReturnedItemsHaveZeroConfirmableItems()
    {
        var purchase = CreateShippedPurchase();
        purchase.MarkCarrierReturned(DateTime.UtcNow);

        var repo = new Mock<IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId>>();
        var store = new Mock<IPhysicalDeliveryStore>();
        store.Setup(s => s.FindCartOwnerAsync(CartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhysicalPurchaseOwner(BuyerId, CartId));
        store.Setup(s => s.GetPurchasesAsync(CartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([purchase]);
        store.Setup(s => s.ExecuteLockedAsync(BuyerId, CartId, It.IsAny<Func<Task<PhysicalDeliveryConfirmationResponse>>>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, Guid, Func<Task<PhysicalDeliveryConfirmationResponse>>, CancellationToken>((_, _, work, _) => work());

        var unitOfWork = new Mock<IUnitOfWork>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.UserId).Returns(BuyerId);

        var handler = new ConfirmMyPhysicalDeliveryCommandHandler(repo.Object, store.Object, unitOfWork.Object, currentUser.Object);

        var act = () => handler.Handle(new ConfirmMyPhysicalDeliveryCommand($"cart:{CartId:D}"), default);

        await act.Should().ThrowAsync<ConflictDomainException>();
    }

    [Fact]
    public async Task BuyerConfirmDelivery_SkipsReturnedItemAndConfirmsEligibleItem()
    {
        var returnedPurchase = CreateShippedPurchase();
        returnedPurchase.MarkCarrierReturned(DateTime.UtcNow);

        var normalPurchase = CreateShippedPurchase();

        var repo = new Mock<IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId>>();
        var store = new Mock<IPhysicalDeliveryStore>();
        store.Setup(s => s.FindCartOwnerAsync(CartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhysicalPurchaseOwner(BuyerId, CartId));
        store.Setup(s => s.GetPurchasesAsync(CartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([returnedPurchase, normalPurchase]);
        store.Setup(s => s.ExecuteLockedAsync(BuyerId, CartId, It.IsAny<Func<Task<PhysicalDeliveryConfirmationResponse>>>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, Guid, Func<Task<PhysicalDeliveryConfirmationResponse>>, CancellationToken>((_, _, work, _) => work());

        var unitOfWork = new Mock<IUnitOfWork>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.UserId).Returns(BuyerId);

        var handler = new ConfirmMyPhysicalDeliveryCommandHandler(repo.Object, store.Object, unitOfWork.Object, currentUser.Object);

        var response = await handler.Handle(new ConfirmMyPhysicalDeliveryCommand($"cart:{CartId:D}"), default);

        response.ConfirmedCount.Should().Be(1);
        response.PurchaseIds.Should().ContainSingle().Which.Should().Be(normalPurchase.Id.Value);

        normalPurchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Delivered);
        returnedPurchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveCarrierReturnCommandHandler_PersistsAndReturnsMetadata()
    {
        var purchase = CreateShippedPurchase();
        purchase.MarkCarrierReturned(DateTime.UtcNow.AddHours(-1));

        var repo = new Mock<IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId>>();
        repo.Setup(r => r.GetByIdAsync(purchase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchase);

        var store = new Mock<IPhysicalDeliveryStore>();
        store.Setup(s => s.FindOwnerAsync(purchase.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhysicalPurchaseOwner(BuyerId, CartId));
        store.Setup(s => s.ExecuteLockedAsync(BuyerId, CartId, It.IsAny<Func<Task<CarrierReturnResolutionResponse>>>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, Guid, Func<Task<CarrierReturnResolutionResponse>>, CancellationToken>((_, _, work, _) => work());

        var unitOfWork = new Mock<IUnitOfWork>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.UserId).Returns(AdminId);

        var handler = new ResolveCarrierReturnCommandHandler(repo.Object, store.Object, unitOfWork.Object, currentUser.Object);

        var response = await handler.Handle(new ResolveCarrierReturnCommand(
            purchase.Id.Value, nameof(CarrierReturnResolution.DeliveryConfirmed), "Resolved by admin call"), default);

        response.PurchaseId.Should().Be(purchase.Id.Value);
        response.DeliveryStatus.Should().Be(nameof(PhysicalDeliveryStatus.Delivered));
        response.CarrierReturnResolution.Should().Be(nameof(CarrierReturnResolution.DeliveryConfirmed));
        response.CarrierReturnResolutionNote.Should().Be("Resolved by admin call");
        response.CarrierReturnResolvedByUserId.Should().Be(AdminId);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveCarrierReturnCommandValidator_ValidatesInput()
    {
        var validator = new ResolveCarrierReturnCommandValidator();

        var valid = new ResolveCarrierReturnCommand(Guid.NewGuid(), "DeliveryConfirmed", "Note");
        (await validator.ValidateAsync(valid)).IsValid.Should().BeTrue();

        var emptyId = new ResolveCarrierReturnCommand(Guid.Empty, "DeliveryConfirmed", "Note");
        (await validator.ValidateAsync(emptyId)).IsValid.Should().BeFalse();

        var invalidRes = new ResolveCarrierReturnCommand(Guid.NewGuid(), "Unknown", "Note");
        (await validator.ValidateAsync(invalidRes)).IsValid.Should().BeFalse();

        var emptyNote = new ResolveCarrierReturnCommand(Guid.NewGuid(), "ClosedWithoutSettlement", "");
        (await validator.ValidateAsync(emptyNote)).IsValid.Should().BeFalse();

        var longNote = new ResolveCarrierReturnCommand(Guid.NewGuid(), "ClosedWithoutSettlement", new string('x', 1001));
        (await validator.ValidateAsync(longNote)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void OrderCarrierReturnStatus_EvaluatesCorrectly()
    {
        var orderId = "cart:" + Guid.NewGuid();

        // 1. No returned items -> None
        var itemNormal = CreateOrderItem("item1", carrierReturnedAtUtc: null, resolution: null);
        var orderNone = CreateOrderDetails(orderId, [itemNormal]);
        OrderCarrierReturnStatus.For(orderNone).Should().Be(OrderCarrierReturnStatus.None);

        // 2. Unresolved return -> PendingReview
        var itemUnresolved = CreateOrderItem("item2", carrierReturnedAtUtc: DateTime.UtcNow, resolution: null);
        var orderPending = CreateOrderDetails(orderId, [itemNormal, itemUnresolved]);
        OrderCarrierReturnStatus.For(orderPending).Should().Be(OrderCarrierReturnStatus.PendingReview);

        // 3. One unresolved and one resolved -> PendingReview takes precedence
        var itemClosed = CreateOrderItem("item3", carrierReturnedAtUtc: DateTime.UtcNow, resolution: nameof(CarrierReturnResolution.ClosedWithoutSettlement));
        var orderMixed = CreateOrderDetails(orderId, [itemUnresolved, itemClosed]);
        OrderCarrierReturnStatus.For(orderMixed).Should().Be(OrderCarrierReturnStatus.PendingReview);

        // 4. ClosedWithoutSettlement -> ClosedWithoutSettlement
        var orderClosed = CreateOrderDetails(orderId, [itemNormal, itemClosed]);
        OrderCarrierReturnStatus.For(orderClosed).Should().Be(OrderCarrierReturnStatus.ClosedWithoutSettlement);

        // 5. DeliveryConfirmed -> DeliveryConfirmed
        var itemConfirmed = CreateOrderItem("item4", carrierReturnedAtUtc: DateTime.UtcNow, resolution: nameof(CarrierReturnResolution.DeliveryConfirmed));
        var orderConfirmed = CreateOrderDetails(orderId, [itemNormal, itemConfirmed]);
        OrderCarrierReturnStatus.For(orderConfirmed).Should().Be(OrderCarrierReturnStatus.DeliveryConfirmed);
    }

    [Fact]
    public async Task AdminOrderDetails_ExposesResolutionMetadata_WhileBuyerOrderDetailsOmitsAdminResolutionNotes()
    {
        var purchase = CreateShippedPurchase();
        var returnedAt = DateTime.UtcNow.AddHours(-3);
        var resolvedAt = DateTime.UtcNow.AddHours(-1);
        purchase.MarkCarrierReturned(returnedAt);
        purchase.ResolveCarrierReturn(CarrierReturnResolution.DeliveryConfirmed, "Admin internal note", AdminId, resolvedAt);

        var buyer = new OrderBuyer(BuyerId, "buyer-ext-123", "Ali Reza");
        var admin = new OrderBuyer(AdminId, "admin-ext-456", "Admin Super");

        var cart = Cart.Create(CartId, BuyerId, "buyer-ext-123", [new CartItemInput("prod-1", ProductItemType.Product, 1)], null);
        cart.SetQuote([new CartQuoteItem("prod-1", "Product 1", 50, 0, 0, 50, ProductItemType.Product, null, 1)], 0);
        cart.MarkPaid();

        var snapshot = new OrderSnapshot([cart], [], [], [buyer, admin], [purchase], []);

        var reader = new Mock<IOrderReader>();
        reader.Setup(r => r.ReadAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        reader.Setup(r => r.ReadAsync(BuyerId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var adminUser = new Mock<ICurrentUserService>();
        adminUser.Setup(u => u.UserId).Returns(AdminId);

        var buyerUser = new Mock<ICurrentUserService>();
        buyerUser.Setup(u => u.UserId).Returns(BuyerId);

        var orderId = OrderIdFormat.For(OrderSourceType.Cart, cart.Id);

        // Admin Details Query
        var adminHandler = new GetAdminOrderQueryHandler(reader.Object, adminUser.Object);
        var adminResult = await adminHandler.Handle(new GetAdminOrderQuery(orderId), default);

        var adminItem = adminResult.Order.Items.Single();
        adminItem.CarrierReturnedAtUtc.Should().Be(returnedAt);
        adminItem.CarrierReturnResolution.Should().Be(nameof(CarrierReturnResolution.DeliveryConfirmed));
        adminItem.CarrierReturnResolutionNote.Should().Be("Admin internal note");
        adminItem.CarrierReturnResolvedAtUtc.Should().Be(resolvedAt);
        adminItem.CarrierReturnResolvedByExternalUserId.Should().Be("admin-ext-456");

        // Buyer Details Query
        var buyerHandler = new GetMyOrderQueryHandler(reader.Object, buyerUser.Object);
        var buyerResult = await buyerHandler.Handle(new GetMyOrderQuery(orderId), default);

        var buyerItem = buyerResult.Items.Single();
        buyerItem.CarrierReturnedAtUtc.Should().Be(returnedAt);
        buyerItem.CarrierReturnResolution.Should().Be(nameof(CarrierReturnResolution.DeliveryConfirmed));
        buyerItem.CarrierReturnResolutionNote.Should().BeNull();
        buyerItem.CarrierReturnResolvedAtUtc.Should().BeNull();
        buyerItem.CarrierReturnResolvedByExternalUserId.Should().BeNull();

        // Receipt
        var receipt = OrderReceiptMapping.Build(adminResult.Order, snapshot)!;
        var receiptItem = receipt.Items.Single();
        receiptItem.CarrierReturnedAtUtc.Should().Be(returnedAt);
        receiptItem.CarrierReturnResolution.Should().Be(nameof(CarrierReturnResolution.DeliveryConfirmed));
    }

    private static PhysicalProductPurchase CreateShippedPurchase()
    {
        var purchase = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(), CartId, new UserId(BuyerId), "prod-1", "Product 1", 1, 50, 50);
        purchase.MarkDebited(CartId);
        purchase.RecordPaymentShares(new PaymentShares(50m, 0m));
        purchase.MarkShipped();
        return purchase;
    }

    private static OrderItem CreateOrderItem(string externalId, DateTime? carrierReturnedAtUtc, string? resolution) =>
        new(
            externalId, "Product", 50m, 50m, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow,
            nameof(OrderFinancialStatus.Paid), nameof(OrderDeliveryStatus.Pending),
            nameof(ProductItemType.Product), null, 1, nameof(PhysicalDeliveryStatus.Shipped),
            0, 0m, 0m, "TRK123", "PostEx", DateTime.UtcNow,
            carrierReturnedAtUtc, resolution);

    private static OrderDetails CreateOrderDetails(string orderId, IReadOnlyList<OrderItem> items) =>
        new(
            orderId, nameof(OrderSourceType.Cart), CartId, DateTime.UtcNow, DateTime.UtcNow,
            nameof(OrderFinancialStatus.Paid), nameof(OrderDeliveryStatus.Pending),
            50m, items.Count, null, null, null, null, items, [], null);
}
