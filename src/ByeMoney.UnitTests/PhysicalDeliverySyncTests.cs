using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Delivery.DTOs;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Modules.Purchases.Delivery.Services;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ByeMoney.UnitTests;

/// <summary>
/// همگام‌سازی تحویل با واسط پستی: انتخاب واسط بر پایهٔ منبع همان قلم و مهلت پشتیبان.
/// واسط و Store ساختگی‌اند تا آزمون به دیتابیس و شبکه وابسته نباشد.
/// </summary>
public sealed class PhysicalDeliverySyncTests
{
    [Fact]
    public async Task DeliveredItemIsMarkedDeliveredThroughItsProviderClient()
    {
        var purchase = CreateShipped(PhysicalProductPurchase.PostexProvider, "12345");
        var store = new FakeStore(CandidateFor(purchase));
        var repository = new FakeRepository(purchase);
        var client = new FakeTrackingClient(PhysicalProductPurchase.PostexProvider, TrackingOutcome.Delivered);

        var changed = await CreateService(store, repository, client).SyncExternalAsync(CancellationToken.None);

        changed.Should().Be(1);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Delivered);
        client.Calls.Should().Be(1);
        client.LastLookup!.ProviderReference.Should().Be("12345");
        store.LastProviders.Should().ContainSingle().Which.Should().Be(PhysicalProductPurchase.PostexProvider);
    }

    [Fact]
    public async Task EachItemIsQueriedOnlyThroughTheClientOfItsOwnProvider()
    {
        var purchase = CreateShipped(PhysicalProductPurchase.PostexProvider, "12345");
        var store = new FakeStore(CandidateFor(purchase));
        var repository = new FakeRepository(purchase);
        var postexClient = new FakeTrackingClient(PhysicalProductPurchase.PostexProvider, TrackingOutcome.InTransit);
        var manualClient = new FakeTrackingClient(PhysicalProductPurchase.ManualProvider, TrackingOutcome.Delivered);

        await CreateService(store, repository, postexClient, manualClient).SyncExternalAsync(CancellationToken.None);

        postexClient.Calls.Should().Be(1);
        manualClient.Calls.Should().Be(0);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
    }

    [Fact]
    public async Task ItemWhoseProviderHasNoClientIsSkipped()
    {
        var purchase = CreateShipped(PhysicalProductPurchase.ManualProvider, reference: null);
        var store = new FakeStore(CandidateFor(purchase));
        var repository = new FakeRepository(purchase);
        var client = new FakeTrackingClient(PhysicalProductPurchase.PostexProvider, TrackingOutcome.Delivered);

        var changed = await CreateService(store, repository, client).SyncExternalAsync(CancellationToken.None);

        changed.Should().Be(0);
        client.Calls.Should().Be(0);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
    }

    [Theory]
    [InlineData(TrackingOutcome.NotFound)]
    [InlineData(TrackingOutcome.NotConfigured)]
    [InlineData(TrackingOutcome.Failed)]
    [InlineData(TrackingOutcome.InTransit)]
    public async Task NonDeliveredOutcomeKeepsItemShipped(TrackingOutcome outcome)
    {
        var purchase = CreateShipped(PhysicalProductPurchase.PostexProvider, "12345");
        var store = new FakeStore(CandidateFor(purchase));
        var repository = new FakeRepository(purchase);
        var client = new FakeTrackingClient(PhysicalProductPurchase.PostexProvider, outcome);

        var changed = await CreateService(store, repository, client).SyncExternalAsync(CancellationToken.None);

        changed.Should().Be(0);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        purchase.CarrierReturnedAtUtc.Should().BeNull();
        repository.SavedCount.Should().Be(0);
    }

    [Fact]
    public async Task RealBarcodePromotesPreparingParcelToShippedWithoutDeliveringIt()
    {
        var purchase = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(), Guid.NewGuid(), new UserId(Guid.NewGuid()),
            "product-1", "Book", 1, 50m, 50m);
        purchase.RememberCarrierParcel(
            PhysicalProductPurchase.PostexProvider, "12345", purchase.RemainingQuantity);
        var alreadyShipped = CreateShipped(PhysicalProductPurchase.PostexProvider, "999");
        var waitingCandidate = CandidateFor(purchase);
        var store = new FakeStore(waitingCandidate, CandidateFor(alreadyShipped))
        {
            AwaitingTracking = [waitingCandidate]
        };
        var repository = new FakeRepository(purchase, alreadyShipped);
        var client = new FakeTrackingClient(
            PhysicalProductPurchase.PostexProvider, TrackingOutcome.Delivered, "IR-200");

        var changed = await CreateService(store, repository, client).SyncExternalAsync(CancellationToken.None);

        changed.Should().Be(2);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        purchase.TrackingCode.Should().Be("IR-200");
        purchase.DeliveredAtUtc.Should().BeNull();
        purchase.ProviderParcelId.Should().Be("12345");
        alreadyShipped.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Delivered);
    }

    [Fact]
    public async Task MaskedBarcodeKeepsPreparingParcelWaiting()
    {
        var purchase = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(), Guid.NewGuid(), new UserId(Guid.NewGuid()),
            "product-1", "Book", 1, 50m, 50m);
        purchase.RememberCarrierParcel(
            PhysicalProductPurchase.PostexProvider, "12345", purchase.RemainingQuantity);
        var store = new FakeStore { AwaitingTracking = [CandidateFor(purchase)] };
        var repository = new FakeRepository(purchase);
        var client = new FakeTrackingClient(PhysicalProductPurchase.PostexProvider, TrackingOutcome.InTransit);

        var changed = await CreateService(store, repository, client).SyncExternalAsync(CancellationToken.None);

        changed.Should().Be(0);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
        purchase.TrackingCode.Should().BeNull();
        purchase.ShippedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task RealBarcodeWithChangedQuantityDoesNotPromoteAndClearsPendingMarker()
    {
        // مرسوله برای دو عدد ساخته شده ولی یک عدد پیش از رسیدن بارکد لغو شده است؛
        // ارتقای خودکار مرسولهٔ دوعددی را «ارسال‌شده» ثبت نمی‌کند.
        var purchase = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(), Guid.NewGuid(), new UserId(Guid.NewGuid()),
            "product-1", "Book", 2, 50m, 100m);
        purchase.RecordPaymentShares(new PaymentShares(100m, 0m));
        purchase.RememberCarrierParcel(PhysicalProductPurchase.PostexProvider, "12345", 2);
        purchase.MarkCarrierPaymentPending(DateTime.UtcNow);
        purchase.RecordCancellation(1, new PaymentShares(50m, 0m));

        var waitingCandidate = CandidateFor(purchase);
        var store = new FakeStore { AwaitingTracking = [waitingCandidate] };
        var repository = new FakeRepository(purchase);
        var client = new FakeTrackingClient(
            PhysicalProductPurchase.PostexProvider, TrackingOutcome.Delivered, "IR-200");

        var changed = await CreateService(store, repository, client).SyncExternalAsync(CancellationToken.None);

        changed.Should().Be(0);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
        purchase.TrackingCode.Should().BeNull();
        purchase.ShippedAtUtc.Should().BeNull();
        purchase.ProviderParcelId.Should().Be("12345");
        purchase.HasPendingCarrierPayment.Should().BeFalse();
    }

    [Fact]
    public async Task ReturnedOutcomeForPreparingParcelDoesNotRecordCarrierReturn()
    {
        // لغو از سوی پشتیبانی واسط نباید روی قلم آماده‌سازی برگشتی ثبت کند؛ ارسال و برگشتی فقط برای Shipped است.
        var purchase = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(), Guid.NewGuid(), new UserId(Guid.NewGuid()),
            "product-1", "Book", 1, 50m, 50m);
        purchase.RememberCarrierParcel(
            PhysicalProductPurchase.PostexProvider, "12345", purchase.RemainingQuantity);
        var waitingCandidate = CandidateFor(purchase);
        var store = new FakeStore(waitingCandidate) { AwaitingTracking = [waitingCandidate] };
        var repository = new FakeRepository(purchase);
        var client = new FakeTrackingClient(PhysicalProductPurchase.PostexProvider, TrackingOutcome.Returned);

        var changed = await CreateService(store, repository, client).SyncExternalAsync(CancellationToken.None);

        changed.Should().Be(0);
        purchase.CarrierReturnedAtUtc.Should().BeNull();
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
    }

    [Fact]
    public async Task ReturnedOutcomePersistsCarrierReturnedOnceWithoutChangingDeliveryStatus()
    {
        var purchase = CreateShipped(PhysicalProductPurchase.PostexProvider, "12345");
        var store = new FakeStore(CandidateFor(purchase));
        var repository = new FakeRepository(purchase);
        var client = new FakeTrackingClient(PhysicalProductPurchase.PostexProvider, TrackingOutcome.Returned);
        var service = CreateService(store, repository, client);

        var first = await service.SyncExternalAsync(CancellationToken.None);
        var markedAt = purchase.CarrierReturnedAtUtc;

        var second = await service.SyncExternalAsync(CancellationToken.None);

        first.Should().Be(1);
        second.Should().Be(0);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        markedAt.Should().NotBeNull();
        purchase.CarrierReturnedAtUtc.Should().Be(markedAt);
        repository.SavedCount.Should().Be(1);
        client.Calls.Should().Be(2);
    }

    [Fact]
    public async Task ExpiredConfirmationSkipsPurchaseAlreadyMarkedCarrierReturned()
    {
        var purchase = CreateShipped(PhysicalProductPurchase.PostexProvider, "12345");
        purchase.MarkCarrierReturned(DateTime.UtcNow.AddDays(-20));
        // Store واقعی CarrierReturned را فیلتر می‌کند؛ اینجا کاندید خالی می‌آید.
        var store = new FakeStore();
        var repository = new FakeRepository(purchase);

        var service = CreateService(
            store, repository, new PhysicalDeliverySettings { AutoConfirmAfterDays = 30 });

        var changed = await service.ConfirmExpiredAsync(CancellationToken.None);

        changed.Should().Be(0);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        purchase.CarrierReturnedAtUtc.Should().NotBeNull();
        repository.SavedCount.Should().Be(0);
    }

    [Fact]
    public async Task DeliverAsyncSkipsPurchaseAlreadyMarkedCarrierReturned()
    {
        var purchase = CreateShipped(PhysicalProductPurchase.PostexProvider, "12345");
        purchase.MarkCarrierReturned(DateTime.UtcNow);
        var store = new FakeStore(CandidateFor(purchase));
        var repository = new FakeRepository(purchase);
        var client = new FakeTrackingClient(PhysicalProductPurchase.PostexProvider, TrackingOutcome.Delivered);

        var changed = await CreateService(store, repository, client).SyncExternalAsync(CancellationToken.None);

        changed.Should().Be(0);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        repository.SavedCount.Should().Be(0);
    }

    [Fact]
    public async Task NoConfiguredClientMeansNoQueryAndNoChange()
    {
        var purchase = CreateShipped(PhysicalProductPurchase.PostexProvider, "12345");
        var store = new FakeStore(CandidateFor(purchase));
        var repository = new FakeRepository(purchase);
        var client = new FakeTrackingClient(PhysicalProductPurchase.PostexProvider, TrackingOutcome.Delivered)
        {
            IsConfigured = false
        };

        var changed = await CreateService(store, repository, client).SyncExternalAsync(CancellationToken.None);

        changed.Should().Be(0);
        store.ShippedQueryCount.Should().Be(0);
        client.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ExpiredConfirmationIsOffWhenThresholdIsZero()
    {
        var purchase = CreateShipped(PhysicalProductPurchase.PostexProvider, "12345");
        var store = new FakeStore(CandidateFor(purchase));
        var repository = new FakeRepository(purchase);

        var service = CreateService(
            store, repository, new PhysicalDeliverySettings { AutoConfirmAfterDays = 0 });

        var changed = await service.ConfirmExpiredAsync(CancellationToken.None);

        changed.Should().Be(0);
        store.ShippedQueryCount.Should().Be(0);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
    }

    [Fact]
    public async Task ExpiredConfirmationUsesThresholdAndCoversAllProviders()
    {
        var purchase = CreateShipped(PhysicalProductPurchase.PostexProvider, "12345");
        var store = new FakeStore(CandidateFor(purchase));
        var repository = new FakeRepository(purchase);

        var service = CreateService(
            store, repository, new PhysicalDeliverySettings { AutoConfirmAfterDays = 30 });

        var changed = await service.ConfirmExpiredAsync(CancellationToken.None);

        changed.Should().Be(1);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Delivered);
        store.LastProviders.Should().BeEmpty();
        store.LastExcludeOpenCarrierAlerts.Should().BeTrue();
        store.LastThreshold.Should().BeCloseTo(DateTime.UtcNow.AddDays(-30), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void DefaultDeliveryWindowIsThirtyDaysAndPollIsFifteenMinutes()
    {
        var settings = new PhysicalDeliverySettings();
        settings.AutoConfirmAfterDays.Should().Be(30);
        settings.PollIntervalMinutes.Should().Be(15);
    }

    [Fact]
    public async Task ExpiredAndProviderDeliverySkipUnresolvedAlertAndClosedWithoutSettlement()
    {
        var unresolved = CreateShipped(PhysicalProductPurchase.PostexProvider, "12345");
        unresolved.MarkCarrierAlert(PhysicalProductPurchase.DamagedParcelStatusCode, DateTime.UtcNow);
        var closed = CreateShipped(PhysicalProductPurchase.PostexProvider, "77");
        closed.MarkCarrierAlert(PhysicalProductPurchase.MissingShipmentStatusCode, DateTime.UtcNow);
        closed.ResolveCarrierAlert(
            CarrierReturnResolution.ClosedWithoutSettlement, "lost", Guid.NewGuid(), DateTime.UtcNow);

        var store = new FakeStore(CandidateFor(unresolved), CandidateFor(closed));
        var repository = new FakeRepository(unresolved, closed);
        var client = new FakeTrackingClient(PhysicalProductPurchase.PostexProvider, TrackingOutcome.Delivered);
        var service = CreateService(
            store, repository, new PhysicalDeliverySettings { AutoConfirmAfterDays = 30 }, client);

        var expired = await service.ConfirmExpiredAsync(CancellationToken.None);
        var synced = await service.SyncExternalAsync(CancellationToken.None);

        expired.Should().Be(0);
        synced.Should().Be(0);
        unresolved.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        closed.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        store.LastExcludeOpenCarrierAlerts.Should().BeFalse();
    }

    [Fact]
    public async Task SyncRecordsAlertOnceOnShippedAndIgnoresPreparing()
    {
        var shipped = CreateShipped(PhysicalProductPurchase.PostexProvider, "12345");
        var preparing = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(), Guid.NewGuid(), new UserId(Guid.NewGuid()),
            "product-1", "Book", 1, 50m, 50m);
        preparing.RememberCarrierParcel(PhysicalProductPurchase.PostexProvider, "88", 1);

        var store = new FakeStore(CandidateFor(shipped))
        {
            AwaitingTracking = [CandidateFor(preparing)]
        };
        var repository = new FakeRepository(shipped, preparing);
        var client = new FakeTrackingClient(PhysicalProductPurchase.PostexProvider, TrackingOutcome.InTransit)
        {
            Result = new TrackingResult(
                TrackingOutcome.InTransit,
                StatusCode: PhysicalProductPurchase.DamagedParcelStatusCode,
                ParentCode: PhysicalProductPurchase.OthersParentCode,
                DurableAlert: true,
                TrackingCode: "CODE-REAL")
        };

        var service = CreateService(store, repository, client);
        var first = await service.SyncExternalAsync(CancellationToken.None);
        var second = await service.SyncExternalAsync(CancellationToken.None);

        first.Should().Be(0);
        second.Should().Be(0);
        shipped.CarrierAlertCode.Should().Be(PhysicalProductPurchase.DamagedParcelStatusCode);
        shipped.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Shipped);
        preparing.CarrierAlertCode.Should().BeNull();
        preparing.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Preparing);
    }

    private static PhysicalDeliverySyncService CreateService(
        FakeStore store,
        FakeRepository repository,
        params ITrackingProviderClient[] clients) =>
        CreateService(store, repository, new PhysicalDeliverySettings(), clients);

    private static PhysicalDeliverySyncService CreateService(
        FakeStore store,
        FakeRepository repository,
        PhysicalDeliverySettings settings,
        params ITrackingProviderClient[] clients) =>
        new(store, repository, repository, clients, settings,
            NullLogger<PhysicalDeliverySyncService>.Instance);

    private static PhysicalProductPurchase CreateShipped(string provider, string? reference)
    {
        var purchase = PhysicalProductPurchase.Create(
            PhysicalProductPurchaseId.New(), Guid.NewGuid(), new UserId(Guid.NewGuid()),
            "product-1", "Book", 1, 50m, 50m);

        purchase.MarkShipped("CODE-1", provider, reference);
        return purchase;
    }

    private static PhysicalDeliveryCandidate CandidateFor(PhysicalProductPurchase purchase) =>
        new(purchase.Id.Value, purchase.BuyerId.Value, purchase.CartId,
            purchase.TrackingCode, purchase.TrackingProvider, purchase.ProviderParcelId, purchase.ShippedAtUtc);

    private sealed class FakeTrackingClient(
        string provider, TrackingOutcome outcome, string? trackingCode = null) : ITrackingProviderClient
    {
        public string Provider => provider;

        public bool IsConfigured { get; init; } = true;

        public int Calls { get; private set; }

        public TrackingLookup? LastLookup { get; private set; }

        public TrackingResult? Result { get; init; }

        public Task<TrackingResult> GetTrackingAsync(TrackingLookup lookup, CancellationToken ct)
        {
            Calls++;
            LastLookup = lookup;
            return Task.FromResult(Result ?? new TrackingResult(outcome, TrackingCode: trackingCode));
        }
    }

    private sealed class FakeStore(params PhysicalDeliveryCandidate[] candidates) : IPhysicalDeliveryStore
    {
        public int ShippedQueryCount { get; private set; }

        public DateTime? LastThreshold { get; private set; }

        public IReadOnlyCollection<string>? LastProviders { get; private set; }

        public Task<PhysicalPurchaseOwner?> FindOwnerAsync(Guid purchaseId, CancellationToken ct) =>
            Task.FromResult<PhysicalPurchaseOwner?>(null);

        public Task<PhysicalPurchaseOwner?> FindCartOwnerAsync(Guid cartId, CancellationToken ct) =>
            Task.FromResult<PhysicalPurchaseOwner?>(null);

        public Task<IReadOnlyList<PhysicalProductPurchase>> GetPurchasesAsync(
            Guid cartId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PhysicalProductPurchase>>([]);

        public Task<ShippingAddress?> GetCartShippingAddressAsync(Guid cartId, CancellationToken ct) =>
            Task.FromResult<ShippingAddress?>(null);

        public Task<string?> GetCartShippingOriginIdAsync(Guid cartId, CancellationToken ct) =>
            Task.FromResult<string?>(null);

        public IReadOnlyList<PhysicalDeliveryCandidate> AwaitingTracking { get; init; } = [];

        public bool? LastExcludeOpenCarrierAlerts { get; private set; }

        public Task<IReadOnlyList<PhysicalDeliveryCandidate>> GetShippedAsync(
            DateTime shippedAtOrBeforeUtc, IReadOnlyCollection<string> providers, int limit,
            bool excludeOpenCarrierAlerts, CancellationToken ct)
        {
            ShippedQueryCount++;
            LastThreshold = shippedAtOrBeforeUtc;
            LastProviders = providers;
            LastExcludeOpenCarrierAlerts = excludeOpenCarrierAlerts;
            return Task.FromResult<IReadOnlyList<PhysicalDeliveryCandidate>>(candidates);
        }

        public Task<IReadOnlyList<PhysicalDeliveryCandidate>> GetPreparingWithCarrierReferenceAsync(
            IReadOnlyCollection<string> providers, int limit, CancellationToken ct) =>
            Task.FromResult(AwaitingTracking);

        public Task<T> ExecuteLockedAsync<T>(Guid buyerId, Guid cartId, Func<Task<T>> work, CancellationToken ct) =>
            work();
    }

    private sealed class FakeRepository(params PhysicalProductPurchase[] purchases) :
        IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId>,
        IUnitOfWork
    {
        private readonly Dictionary<Guid, PhysicalProductPurchase> items =
            purchases.ToDictionary(purchase => purchase.Id.Value);

        public int SavedCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SavedCount++;
            return Task.FromResult(SavedCount);
        }

        public Task<PhysicalProductPurchase?> GetByIdAsync(
            PhysicalProductPurchaseId id, CancellationToken ct = default) =>
            Task.FromResult(items.GetValueOrDefault(id.Value));

        public Task<List<PhysicalProductPurchase>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult(items.Values.ToList());

        public Task AddAsync(PhysicalProductPurchase entity, CancellationToken ct = default)
        {
            items[entity.Id.Value] = entity;
            return Task.CompletedTask;
        }

        public void Update(PhysicalProductPurchase entity) => items[entity.Id.Value] = entity;

        public void Remove(PhysicalProductPurchase entity) => items.Remove(entity.Id.Value);
    }
}
