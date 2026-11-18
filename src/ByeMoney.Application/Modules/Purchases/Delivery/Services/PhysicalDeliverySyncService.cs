using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Delivery.DTOs;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Domain.Modules.Purchases;
using Microsoft.Extensions.Logging;

namespace ByeMoney.Application.Modules.Purchases.Delivery.Services;

/// <summary>
/// تحویل خودکار قلم‌های «ارسال‌شده» (SHIP-TRACKING): هر قلم جدا و idempotent.
/// دو منبع دارد: وضعیت واسط پستیِ همان قلم (استعلام بیرونی، خارج از قفل) و مهلت زمانی پشتیبان.
/// هیچ‌کدام اثر مالی ندارند؛ برگشتیِ واسط فقط علامت می‌شود و تسویه را می‌بندد؛ لغو فقط از Preparing ممکن است.
/// </summary>
public sealed class PhysicalDeliverySyncService(
    IPhysicalDeliveryStore store,
    IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId> repository,
    IUnitOfWork unitOfWork,
    IEnumerable<ITrackingProviderClient> trackingClients,
    PhysicalDeliverySettings settings,
    ILogger<PhysicalDeliverySyncService> logger)
{
    public const int BatchSize = 50;

    /// <summary>قلم‌هایی که بیش از مهلت تنظیم‌شده در Shipped مانده‌اند را تحویل‌شده می‌کند.</summary>
    public async Task<int> ConfirmExpiredAsync(CancellationToken ct)
    {
        if (settings.AutoConfirmAfterDays <= 0)
        {
            return 0;
        }

        var threshold = DateTime.UtcNow.AddDays(-settings.AutoConfirmAfterDays);
        // GetShippedAsync قلم‌های دارای CarrierReturnedAtUtc را نمی‌آورد.
        var candidates = await store.GetShippedAsync(threshold, providers: [], BatchSize, ct);
        if (candidates.Count == 0)
        {
            return 0;
        }

        return await DeliverAsync(candidates, "expired", ct);
    }

    /// <summary>وضعیت هر مرسوله را از واسط پستیِ خودش می‌پرسد و تحویل‌شده‌ها یا برگشتی‌ها را ثبت می‌کند.</summary>
    public async Task<int> SyncExternalAsync(CancellationToken ct)
    {
        var clients = ConfiguredClients();
        if (clients.Count == 0)
        {
            return 0;
        }

        // زمان Shipped شرط انتخاب نیست؛ مرسولهٔ تازه‌هم استعلام می‌شود.
        var providers = clients.Keys.ToArray();
        var promotedIds = await PromoteWhenTrackingCodeArrivesAsync(clients, providers, ct);
        var candidates = await store.GetShippedAsync(DateTime.UtcNow, providers, BatchSize, ct);
        if (candidates.Count == 0)
        {
            return promotedIds.Count;
        }

        var delivered = new List<PhysicalDeliveryCandidate>();
        var returned = 0;
        foreach (var candidate in candidates)
        {
            // قلمی که همین نوبت ارسال‌شده شده، تا نوبت بعد تحویل‌شده نمی‌شود.
            if (promotedIds.Contains(candidate.PurchaseId))
            {
                continue;
            }

            if (candidate.TrackingProvider is null ||
                !clients.TryGetValue(candidate.TrackingProvider, out var client))
            {
                logger.LogWarning(
                    "No tracking provider client is available for purchase {PurchaseId} (provider {Provider}).",
                    candidate.PurchaseId, candidate.TrackingProvider);
                continue;
            }

            var result = await client.GetTrackingAsync(
                new TrackingLookup(candidate.ProviderParcelId, candidate.TrackingCode), ct);

            switch (result.Outcome)
            {
                case TrackingOutcome.Delivered:
                    delivered.Add(candidate);
                    break;

                case TrackingOutcome.Returned:
                    returned += await MarkReturnedAsync(candidate, client.Provider, ct);
                    break;

                case TrackingOutcome.NotFound:
                    logger.LogWarning(
                        "{Provider} tracking not found for purchase {PurchaseId} (reference {Reference}).",
                        client.Provider, candidate.PurchaseId, candidate.ProviderParcelId);
                    break;

                case TrackingOutcome.NotConfigured:
                case TrackingOutcome.Failed:
                    // تنظیم ناقص یا خطای موقت واسط، وضعیت را عوض نمی‌کند؛ چرخهٔ بعدی دوباره تلاش می‌کند.
                    logger.LogWarning(
                        "{Provider} tracking check failed for purchase {PurchaseId} (reference {Reference}).",
                        client.Provider, candidate.PurchaseId, candidate.ProviderParcelId);
                    break;
            }
        }

        var deliveredCount = delivered.Count == 0 ? 0 : await DeliverAsync(delivered, "provider", ct);
        return deliveredCount + returned + promotedIds.Count;
    }

    /// <summary>
    /// خریدِ هنوز «آماده‌سازی» که شناسهٔ ارجاع دارد، با آمدن بارکد واقعی فقط «ارسال‌شده» می‌شود.
    /// حتی اگر همان پاسخ تحویل را هم بگوید، در این مرحله به «تحویل‌شده» نمی‌رود.
    /// </summary>
    private async Task<HashSet<Guid>> PromoteWhenTrackingCodeArrivesAsync(
        Dictionary<string, ITrackingProviderClient> clients,
        string[] providers,
        CancellationToken ct)
    {
        var waiting = await store.GetPreparingWithCarrierReferenceAsync(providers, BatchSize, ct);
        var promoted = new HashSet<Guid>();
        foreach (var candidate in waiting)
        {
            if (candidate.TrackingProvider is null ||
                !clients.TryGetValue(candidate.TrackingProvider, out var client))
            {
                continue;
            }

            var result = await client.GetTrackingAsync(
                new TrackingLookup(candidate.ProviderParcelId, candidate.TrackingCode), ct);
            if (!PhysicalProductPurchase.IsUsablePostalTrackingCode(result.TrackingCode))
            {
                continue;
            }

            var changed = await store.ExecuteLockedAsync(candidate.BuyerId, candidate.CartId, async () =>
            {
                var purchase = await repository.GetByIdAsync(
                    new PhysicalProductPurchaseId(candidate.PurchaseId), ct);
                if (purchase is null || purchase.DeliveryStatus != PhysicalDeliveryStatus.Preparing)
                {
                    return 0;
                }

                purchase.MarkShipped(result.TrackingCode, client.Provider, purchase.ProviderParcelId);
                repository.Update(purchase);
                await unitOfWork.SaveChangesAsync(ct);
                return 1;
            }, ct);
            if (changed > 0)
            {
                promoted.Add(candidate.PurchaseId);
            }
        }

        return promoted;
    }

    private Dictionary<string, ITrackingProviderClient> ConfiguredClients() =>
        trackingClients
            .Where(client => client.IsConfigured && !string.IsNullOrWhiteSpace(client.Provider))
            .GroupBy(client => client.Provider, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    private async Task<int> MarkReturnedAsync(
        PhysicalDeliveryCandidate candidate, string provider, CancellationToken ct)
    {
        var changed = await store.ExecuteLockedAsync(candidate.BuyerId, candidate.CartId, async () =>
        {
            var purchase = await repository.GetByIdAsync(
                new PhysicalProductPurchaseId(candidate.PurchaseId), ct);
            if (purchase is null || purchase.CarrierReturnedAtUtc.HasValue)
            {
                return 0;
            }

            purchase.MarkCarrierReturned(DateTime.UtcNow);
            repository.Update(purchase);
            await unitOfWork.SaveChangesAsync(ct);
            return 1;
        }, ct);

        if (changed > 0)
        {
            logger.LogWarning(
                "{Provider} tracking returned for purchase {PurchaseId} (reference {Reference}); settlement blocked until manual review.",
                provider, candidate.PurchaseId, candidate.ProviderParcelId);
        }

        return changed;
    }

    private async Task<int> DeliverAsync(
        IReadOnlyList<PhysicalDeliveryCandidate> candidates, string source, CancellationToken ct)
    {
        var delivered = 0;
        foreach (var candidate in candidates)
        {
            var changed = await store.ExecuteLockedAsync(candidate.BuyerId, candidate.CartId, async () =>
            {
                var purchase = await repository.GetByIdAsync(
                    new PhysicalProductPurchaseId(candidate.PurchaseId), ct);
                if (purchase is null ||
                    purchase.DeliveryStatus != PhysicalDeliveryStatus.Shipped ||
                    purchase.CarrierReturnedAtUtc.HasValue)
                {
                    return 0;
                }

                purchase.MarkDelivered();
                repository.Update(purchase);
                await unitOfWork.SaveChangesAsync(ct);
                return 1;
            }, ct);

            delivered += changed;
        }

        if (delivered > 0)
        {
            logger.LogInformation("Physical delivery marked Delivered by {Source} for {Count} item(s).", source, delivered);
        }

        return delivered;
    }
}
