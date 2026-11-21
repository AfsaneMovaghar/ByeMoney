using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Commands.RefreshPhysicalShipment;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using FluentAssertions;

namespace ByeMoney.UnitTests;

/// <summary>
/// «بررسی وضعیت» مرسولهٔ پرداخت‌نشده: بدون ساخت، بدون اثر مالی، و با نگهبان تعداد.
/// </summary>
public sealed class RefreshPhysicalShipmentTests
{
    [Fact]
    public async Task PendingPaymentIsReportedWithoutChangingAnything()
    {
        var purchase = CreatePendingPayment(quantity: 1, parcelQuantity: 1);
        var paidAmount = purchase.PaidAmountInNoor;
        var pendingSince = purchase.CarrierPaymentPendingSinceUtc;
        var client = new FakeShipmentClient
        {
            ReadResult = new ShipmentCreateResult(ShipmentCreateOutcome.PendingPayment, "99")
        };

        var response = await CreateHandler(purchase, client)
            .Handle(new RefreshPhysicalShipmentCommand(purchase.Id.Value), CancellationToken.None);

        response.Outcome.Should().Be(nameof(PhysicalShipmentRefreshOutcome.PaymentPending));
        response.DeliveryStatus.Should().Be(nameof(PhysicalDeliveryStatus.Preparing));
        response.ProviderParcelId.Should().Be("99");
        response.Warning.Should().Be(OrderErrors.ShipmentPaymentPendingHelp);
        purchase.CarrierPaymentPendingSinceUtc.Should().Be(pendingSince);
        purchase.PaidAmountInNoor.Should().Be(paidAmount);
        purchase.RefundedRegularInNoor.Should().Be(0m);
        client.Calls.Should().Be(1);
        client.LastKey.Should().Be($"BM-{purchase.CartId:N}");
    }

    [Fact]
    public async Task AcceptedParcelWithRealBarcodeIsMarkedShipped()
    {
        var purchase = CreatePendingPayment(quantity: 1, parcelQuantity: 1);
        var client = new FakeShipmentClient
        {
            ReadResult = new ShipmentCreateResult(ShipmentCreateOutcome.Created, "99", "IR-100")
        };

        var response = await CreateHandler(purchase, client)
            .Handle(new RefreshPhysicalShipmentCommand(purchase.Id.Value), CancellationToken.None);

        response.Outcome.Should().Be(nameof(PhysicalShipmentRefreshOutcome.Shipped));
        response.DeliveryStatus.Should().Be(nameof(PhysicalDeliveryStatus.Shipped));
        response.TrackingCode.Should().Be("IR-100");
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        purchase.HasPendingCarrierPayment.Should().BeFalse();
        purchase.DeliveredAtUtc.Should().BeNull();
        purchase.RefundedRegularInNoor.Should().Be(0m);
    }

    [Fact]
    public async Task AcceptedParcelWithChangedQuantityIsNotPromoted()
    {
        var purchase = CreatePendingPayment(quantity: 2, parcelQuantity: 2);
        purchase.RecordCancellation(1, new PaymentShares(50m, 0m));
        var client = new FakeShipmentClient
        {
            ReadResult = new ShipmentCreateResult(ShipmentCreateOutcome.Created, "99", "IR-100")
        };

        var response = await CreateHandler(purchase, client)
            .Handle(new RefreshPhysicalShipmentCommand(purchase.Id.Value), CancellationToken.None);

        response.Outcome.Should().Be(nameof(PhysicalShipmentRefreshOutcome.AwaitingTrackingCode));
        response.Warning.Should().Be(OrderErrors.ShipmentQuantityMismatch);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
        purchase.TrackingCode.Should().BeNull();
        purchase.ShippedAtUtc.Should().BeNull();
        purchase.HasPendingCarrierPayment.Should().BeFalse();
        purchase.RefundedRegularInNoor.Should().Be(50m);
    }

    [Fact]
    public async Task UnknownParcelClearsTheReferenceSoCreationIsAllowedAgain()
    {
        var purchase = CreatePendingPayment(quantity: 1, parcelQuantity: 1);
        var client = new FakeShipmentClient
        {
            ReadResult = new ShipmentCreateResult(ShipmentCreateOutcome.NotFound)
        };

        var response = await CreateHandler(purchase, client)
            .Handle(new RefreshPhysicalShipmentCommand(purchase.Id.Value), CancellationToken.None);

        response.Outcome.Should().Be(nameof(PhysicalShipmentRefreshOutcome.ParcelNotFound));
        response.ProviderParcelId.Should().BeNull();
        purchase.ProviderParcelId.Should().BeNull();
        purchase.TrackingProvider.Should().BeNull();
        purchase.HasPendingCarrierPayment.Should().BeFalse();
        purchase.HasCarrierParcel.Should().BeFalse();
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
    }

    [Fact]
    public async Task ReadFailureChangesNothing()
    {
        var purchase = CreatePendingPayment(quantity: 1, parcelQuantity: 1);
        var client = new FakeShipmentClient
        {
            ReadResult = new ShipmentCreateResult(ShipmentCreateOutcome.Failed)
        };

        var response = await CreateHandler(purchase, client)
            .Handle(new RefreshPhysicalShipmentCommand(purchase.Id.Value), CancellationToken.None);

        response.Outcome.Should().Be(nameof(PhysicalShipmentRefreshOutcome.ReadFailed));
        response.Warning.Should().Be(OrderErrors.ShipmentStatusUnavailable);
        purchase.ProviderParcelId.Should().Be("99");
        purchase.HasPendingCarrierPayment.Should().BeTrue();
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
    }

    [Fact]
    public async Task RejectsWhenPurchaseHasNoCarrierParcel()
    {
        var purchase = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(), Guid.NewGuid(), new UserId(Guid.NewGuid()),
            "product-1", "Book", 1, 50m, 50m);
        var client = new FakeShipmentClient();

        var act = () => CreateHandler(purchase, client)
            .Handle(new RefreshPhysicalShipmentCommand(purchase.Id.Value), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>())
            .Which.Message.Should().Be(OrderErrors.ShipmentRefreshNotAllowed);
        client.Calls.Should().Be(0);
    }

    private static RefreshPhysicalShipmentCommandHandler CreateHandler(
        PhysicalProductPurchase purchase, FakeShipmentClient client)
    {
        var repository = new FakeRepository(purchase);
        return new RefreshPhysicalShipmentCommandHandler(
            repository, new FakeDeliveryStore(purchase), [client], repository);
    }

    /// <summary>قلم «در انتظار پرداخت» با سهم پرداخت واقعی، آمادهٔ بررسی وضعیت.</summary>
    private static PhysicalProductPurchase CreatePendingPayment(int quantity, int parcelQuantity)
    {
        var purchase = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(), Guid.NewGuid(), new UserId(Guid.NewGuid()),
            "product-1", "Book", quantity, 50m, 50m * quantity);
        purchase.RecordPaymentShares(new PaymentShares(50m * quantity, 0m));
        purchase.RememberCarrierParcel(PhysicalProductPurchase.PostexProvider, "99", parcelQuantity);
        purchase.MarkCarrierPaymentPending(DateTime.UtcNow);
        return purchase;
    }

    private sealed class FakeShipmentClient : IShipmentProviderClient
    {
        public string Provider => PhysicalProductPurchase.PostexProvider;

        public bool IsConfigured => true;

        public string? LastKey { get; private set; }

        public int Calls { get; private set; }

        public ShipmentCreateResult ReadResult { get; init; } =
            new(ShipmentCreateOutcome.NotFound);

        public Task<ShipmentCreateResult> CreateOrGetParcelAsync(
            ShipmentCreateRequest request, CancellationToken ct) =>
            throw new InvalidOperationException("بررسی وضعیت نباید مرسوله بسازد.");

        public Task<ShipmentCreateResult> GetParcelAsync(string idempotencyKey, CancellationToken ct)
        {
            Calls++;
            LastKey = idempotencyKey;
            return Task.FromResult(ReadResult);
        }
    }

    private sealed class FakeDeliveryStore(PhysicalProductPurchase purchase) : IPhysicalDeliveryStore
    {
        public Task<PhysicalPurchaseOwner?> FindOwnerAsync(Guid purchaseId, CancellationToken ct) =>
            Task.FromResult<PhysicalPurchaseOwner?>(
                purchaseId == purchase.Id.Value
                    ? new PhysicalPurchaseOwner(purchase.BuyerId.Value, purchase.CartId)
                    : null);

        public Task<PhysicalPurchaseOwner?> FindCartOwnerAsync(Guid cartId, CancellationToken ct) =>
            Task.FromResult<PhysicalPurchaseOwner?>(null);

        public Task<IReadOnlyList<PhysicalProductPurchase>> GetPurchasesAsync(
            Guid cartId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PhysicalProductPurchase>>([purchase]);

        public Task<ShippingAddress?> GetCartShippingAddressAsync(Guid cartId, CancellationToken ct) =>
            Task.FromResult<ShippingAddress?>(null);

        public Task<string?> GetCartShippingOriginIdAsync(Guid cartId, CancellationToken ct) =>
            Task.FromResult<string?>(null);

        public Task<int?> GetCartShippingOriginCityIdAsync(Guid cartId, CancellationToken ct) =>
            Task.FromResult<int?>(null);

        public Task<IReadOnlyList<PhysicalDeliveryCandidate>> GetShippedAsync(
            DateTime shippedAtOrBeforeUtc, IReadOnlyCollection<string> providers, int limit,
            bool excludeOpenCarrierAlerts, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PhysicalDeliveryCandidate>>([]);

        public Task<IReadOnlyList<PhysicalDeliveryCandidate>> GetPreparingWithCarrierReferenceAsync(
            IReadOnlyCollection<string> providers, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PhysicalDeliveryCandidate>>([]);

        public Task<T> ExecuteLockedAsync<T>(
            Guid buyerId, Guid cartId, Func<Task<T>> work, CancellationToken ct) => work();
    }

    private sealed class FakeRepository(PhysicalProductPurchase purchase) :
        IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId>,
        IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);

        public Task<PhysicalProductPurchase?> GetByIdAsync(
            PhysicalProductPurchaseId id, CancellationToken ct = default) =>
            Task.FromResult(id.Value == purchase.Id.Value ? purchase : null);

        public Task<List<PhysicalProductPurchase>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult(new List<PhysicalProductPurchase> { purchase });

        public Task AddAsync(PhysicalProductPurchase entity, CancellationToken ct = default) =>
            Task.CompletedTask;

        public void Update(PhysicalProductPurchase entity) { }

        public void Remove(PhysicalProductPurchase entity) { }
    }
}
