using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Commands.CreatePhysicalShipment;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Application.Resources;
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
            .Handle(new CreatePhysicalShipmentCommand(purchase.Id.Value, "pick_up"), CancellationToken.None);

        response.DeliveryStatus.Should().Be(nameof(PhysicalDeliveryStatus.Shipped));
        response.ProviderParcelId.Should().Be("1319906676294");
        response.TrackingCode.Should().Be("TRACK-1");
        response.TrackingProvider.Should().Be(PhysicalProductPurchase.PostexProvider);
        response.Outcome.Should().Be(nameof(ShipmentCreateOutcome.Created));
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        client.LastRequest!.Parcel.IdempotencyKey.Should().Be($"BM-{purchase.Id.Value:N}");
        client.LastRequest.Parcel.Quantity.Should().Be(1);
        client.LastRequest.Parcel.Facts.Should().Be(SampleParcel());
        client.LastRequest.Recipient.CityName.Should().Be("مشهد");
        client.LastRequest.Recipient.CityDocumentId.Should().Be("city-mashhad");
        client.LastRequest.OriginId.Should().Be("ramsar");
        client.LastRequest.CollectionType.Should().Be("pick_up");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("**********")]
    public async Task ParcelReferenceWithoutPostalCodeStaysPreparing(string? trackingCode)
    {
        var purchase = CreatePreparing();
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(ShipmentCreateOutcome.Created, "1319906676294", trackingCode)
        };

        var response = await new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.Id.Value, "courier_drop_off"), CancellationToken.None);

        response.DeliveryStatus.Should().Be(nameof(PhysicalDeliveryStatus.Preparing));
        response.Warning.Should().Be(OrderErrors.ShipmentTrackingCodePending);
        response.Outcome.Should().Be("AwaitingTrackingCode");
        response.ProviderParcelId.Should().Be("1319906676294");
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
        purchase.ProviderParcelId.Should().Be("1319906676294");
        purchase.TrackingCode.Should().BeNull();
        purchase.ShippedAtUtc.Should().BeNull();
        client.LastRequest!.CollectionType.Should().Be("courier_drop_off");
    }

    [Fact]
    public async Task LaterRealCodeShipsTheSameParcelWithoutDeliveringIt()
    {
        var purchase = CreatePreparing();
        purchase.RememberCarrierParcel(PhysicalProductPurchase.PostexProvider, "1319906676294");
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(
                ShipmentCreateOutcome.AlreadyExists, "1319906676294", "IR-POST-100")
        };

        var response = await new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.Id.Value, "pick_up"), CancellationToken.None);

        response.DeliveryStatus.Should().Be(nameof(PhysicalDeliveryStatus.Shipped));
        response.TrackingCode.Should().Be("IR-POST-100");
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        purchase.DeliveredAtUtc.Should().BeNull();
        purchase.ProviderParcelId.Should().Be("1319906676294");
        client.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ForwardsLegacyAddressWithoutCityDocumentId()
    {
        var purchase = CreatePreparing();
        var address = new ShippingAddress(
            "ver", "خراسان رضوی", "مشهد", null, "خیابان نمونه", "علی محمدی", "09121111111");
        var store = new FakeDeliveryStore(purchase, address);
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(ShipmentCreateOutcome.Created, "1")
        };

        await new CreatePhysicalShipmentCommandHandler(repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.Id.Value, "pick_up"), CancellationToken.None);

        client.Calls.Should().Be(1);
        client.LastRequest!.Recipient.CityDocumentId.Should().BeNull();
        client.LastRequest.Recipient.ProvinceName.Should().Be("خراسان رضوی");
        client.LastRequest.Recipient.CityName.Should().Be("مشهد");
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
            .Handle(new CreatePhysicalShipmentCommand(purchase.Id.Value, "pick_up"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        client.Calls.Should().Be(0);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
    }

    [Fact]
    public async Task RejectsLegacyPurchaseWithoutParcelFacts()
    {
        var purchase = CreatePreparingWithoutParcel();
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider);

        var act = () => new CreatePhysicalShipmentCommandHandler(
            repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.Id.Value, "pick_up"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>())
            .Which.Message.Should().Be(OrderErrors.ShipmentParcelRequired);
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
            .Handle(new CreatePhysicalShipmentCommand(purchase.Id.Value, "pick_up"), CancellationToken.None);

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
            .Handle(new CreatePhysicalShipmentCommand(purchase.Id.Value, "pick_up"), CancellationToken.None);

        response.Outcome.Should().Be(nameof(ShipmentCreateOutcome.AlreadyExists));
        response.ProviderParcelId.Should().Be("111");
        client.Calls.Should().Be(0);
    }

    [Fact]
    public async Task UnpaidParcelStaysPreparingAndIsNotSettleableAsShipped()
    {
        var purchase = CreatePreparing();
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(
                ShipmentCreateOutcome.PendingPayment, "99", ErrorMessage: OrderErrors.ShipmentPaymentPending)
        };

        var act = () => new CreatePhysicalShipmentCommandHandler(repository, store, [client], repository)
            .Handle(new CreatePhysicalShipmentCommand(purchase.Id.Value, "pick_up"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainException>())
            .Which.Message.Should().Be(OrderErrors.ShipmentPaymentPending);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
        purchase.ProviderParcelId.Should().BeNull();
        client.Calls.Should().Be(1);
    }

    [Fact]
    public async Task SecondCreateWhileFirstIsInFlightDoesNotCallProviderAgain()
    {
        var purchase = CreatePreparing();
        var store = new FakeDeliveryStore(purchase, SampleAddress());
        var repository = new FakeRepository(purchase);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new FakeShipmentClient(PhysicalProductPurchase.PostexProvider)
        {
            Result = new ShipmentCreateResult(ShipmentCreateOutcome.Created, "99", "TRACK-1"),
            BeforeResult = _ =>
            {
                entered.TrySetResult();
                return release.Task;
            }
        };
        var handler = new CreatePhysicalShipmentCommandHandler(repository, store, [client], repository);
        var command = new CreatePhysicalShipmentCommand(purchase.Id.Value, "pick_up");

        var first = handler.Handle(command, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = handler.Handle(command, CancellationToken.None);
        await Task.Delay(50);
        client.Calls.Should().Be(1);

        release.TrySetResult();
        await Task.WhenAll(first, second);

        client.Calls.Should().Be(1);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        purchase.ProviderParcelId.Should().Be("99");
    }

    private static PhysicalProductPurchase CreatePreparing() =>
        CreatePreparing(SampleParcel());

    private static PhysicalProductPurchase CreatePreparingWithoutParcel() =>
        CreatePreparing(null);

    private static PhysicalProductPurchase CreatePreparing(ParcelFacts? parcel) =>
        PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(), Guid.NewGuid(), new UserId(Guid.NewGuid()),
            "product-1", "شمش", 1, 100m, 100m, parcel);

    private static ParcelFacts SampleParcel() =>
        new(
            WeightGrams: 750,
            LengthCm: 15,
            WidthCm: 10,
            HeightCm: 5,
            DeclaredValueIrr: 60_000_000,
            IsFragile: true);

    private static ShippingAddress SampleAddress() =>
        new("ver", "خراسان رضوی", "مشهد", "1234567890", "خیابان نمونه", "علی محمدی", "09121111111", "city-mashhad");

    private sealed class FakeShipmentClient(string provider) : IShipmentProviderClient
    {
        public string Provider => provider;

        public bool IsConfigured { get; init; } = true;

        private int _calls;

        public int Calls => _calls;

        public ShipmentCreateRequest? LastRequest { get; private set; }

        public ShipmentCreateResult Result { get; init; } =
            new(ShipmentCreateOutcome.Failed, ErrorMessage: "not set");

        public Func<CancellationToken, Task>? BeforeResult { get; init; }

        public async Task<ShipmentCreateResult> CreateOrGetParcelAsync(
            ShipmentCreateRequest request, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            LastRequest = request;
            if (BeforeResult is not null)
            {
                await BeforeResult(ct);
            }

            return Result;
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

        public Task<IReadOnlyList<PhysicalDeliveryCandidate>> GetPreparingWithCarrierReferenceAsync(
            IReadOnlyCollection<string> providers, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PhysicalDeliveryCandidate>>([]);

        private readonly SemaphoreSlim _gate = new(1, 1);

        public async Task<T> ExecuteLockedAsync<T>(Guid buyerId, Guid cartId, Func<Task<T>> work, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                return await work();
            }
            finally
            {
                _gate.Release();
            }
        }
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
