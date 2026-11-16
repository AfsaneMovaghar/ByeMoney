using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Commands.CreatePhysicalShipment;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using FluentAssertions;

namespace ByeMoney.UnitTests;

public sealed class CreatePhysicalShipmentTests
{
    [Fact]
    public async Task CreatesParcelAndMarksShippedThroughProviderPort()
    {
        var purchase = CreatePreparing();
        var address = SampleAddress();
        var store = new FakeDeliveryStore(purchase, address);
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(
                ShipmentCreateOutcome.Created, "1319906676294", "TRACK-1", "PAYMENT_PENDING")
        };

        var response = await new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.Id.Value), CancellationToken.None);

        response.DeliveryStatus.Should().Be(nameof(PhysicalDeliveryStatus.Shipped));
        response.ProviderParcelId.Should().Be("1319906676294");
        response.TrackingCode.Should().Be("TRACK-1");
        response.TrackingProvider.Should().Be(PhysicalProductPurchase.PostexProvider);
        response.Outcome.Should().Be(nameof(ShipmentCreateOutcome.Created));
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        client.LastRequest!.Parcel.IdempotencyKey.Should().Be($"BM-{purchase.Id.Value:N}");
        client.LastRequest.Recipient.CityName.Should().Be("مشهد");
        client.LastRequest.OriginId.Should().Be("ramsar");
    }

    [Fact]
    public async Task RejectsWhenCartOriginIsMissing()
    {
        var purchase = CreatePreparing();
        var store = new FakeDeliveryStore(purchase, SampleAddress(), originId: null);
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider);

        var act = () => new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.Id.Value), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        client.Calls.Should().Be(0);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
    }

    [Fact]
    public async Task RejectsWhenProviderIsNotConfigured()
    {
        var purchase = CreatePreparing();
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider) { IsConfigured = false };

        var act = () => new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.Id.Value), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
    }

    [Fact]
    public async Task ReturnsExistingShipmentWithoutCallingProviderAgain()
    {
        var purchase = CreatePreparing();
        purchase.MarkShipped("OLD", PhysicalProductPurchase.PostexProvider, "111");
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider);

        var response = await new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.Id.Value), CancellationToken.None);

        response.Outcome.Should().Be(nameof(ShipmentCreateOutcome.AlreadyExists));
        response.ProviderParcelId.Should().Be("111");
        client.Calls.Should().Be(0);
    }

    private static PhysicalProductPurchase CreatePreparing() =>
        PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(), Guid.NewGuid(), new UserId(Guid.NewGuid()),
            "product-1", "شمش", 1, 100m, 100m);

    private static ShippingAddress SampleAddress() =>
        new("ver", "خراسان رضوی", "مشهد", "1234567890", "خیابان نمونه", "علی محمدی", "09121111111");

    private sealed class FakeShipmentClient(string provider) : IShipmentProviderClient
    {
        public string Provider => provider;

        public bool IsConfigured { get; init; } = true;

        public int Calls { get; private set; }

        public ShipmentCreateRequest? LastRequest { get; private set; }

        public ShipmentCreateResult Result { get; init; } =
            new(ShipmentCreateOutcome.Failed, ErrorMessage: "not set");

        public Task<ShipmentCreateResult> CreateOrGetParcelAsync(
            ShipmentCreateRequest request, CancellationToken ct)
        {
            Calls++;
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeDeliveryStore(
        PhysicalProductPurchase purchase,
        ShippingAddress? address,
        string? originId = "ramsar") : IPhysicalDeliveryStore
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
            Task.FromResult(address);

        public Task<string?> GetCartShippingOriginIdAsync(Guid cartId, CancellationToken ct) =>
            Task.FromResult(originId);

        public Task<IReadOnlyList<PhysicalDeliveryCandidate>> GetShippedAsync(
            DateTime shippedAtOrBeforeUtc, IReadOnlyCollection<string> providers, int limit,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PhysicalDeliveryCandidate>>([]);

        public Task<T> ExecuteLockedAsync<T>(Guid buyerId, Guid cartId, Func<Task<T>> work, CancellationToken ct) =>
            work();
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
