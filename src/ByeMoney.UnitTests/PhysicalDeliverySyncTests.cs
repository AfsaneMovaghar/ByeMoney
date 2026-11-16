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
    [InlineData(TrackingOutcome.Returned)]
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
            store, repository, new PhysicalDeliverySettings { AutoConfirmAfterDays = 14 });

        var changed = await service.ConfirmExpiredAsync(CancellationToken.None);

        changed.Should().Be(1);
        purchase.DeliveryStatus.Should().Be(PhysicalDeliveryStatus.Delivered);
        store.LastProviders.Should().BeEmpty();
        store.LastThreshold.Should().BeCloseTo(DateTime.UtcNow.AddDays(-14), TimeSpan.FromMinutes(5));
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

    private sealed class FakeTrackingClient(string provider, TrackingOutcome outcome) : ITrackingProviderClient
    {
        public string Provider => provider;

        public bool IsConfigured { get; init; } = true;

        public int Calls { get; private set; }

        public TrackingLookup? LastLookup { get; private set; }

        public Task<TrackingResult> GetTrackingAsync(TrackingLookup lookup, CancellationToken ct)
        {
            Calls++;
            LastLookup = lookup;
            return Task.FromResult(new TrackingResult(outcome));
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

        public Task<IReadOnlyList<PhysicalDeliveryCandidate>> GetShippedAsync(
            DateTime shippedAtOrBeforeUtc, IReadOnlyCollection<string> providers, int limit, CancellationToken ct)
        {
            ShippedQueryCount++;
            LastThreshold = shippedAtOrBeforeUtc;
            LastProviders = providers;
            return Task.FromResult<IReadOnlyList<PhysicalDeliveryCandidate>>(candidates);
        }

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
