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
        // GetShippedAsync قلم‌های دارای CarrierReturnedAtUtc و هشدارِ باز را برای تحویل خودکار نمی‌آورد.
        var candidates = await store.GetShippedAsync(
            threshold, providers: [], BatchSize, excludeOpenCarrierAlerts: true, ct);
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
        var candidates = await store.GetShippedAsync(
            DateTime.UtcNow, providers, BatchSize, excludeOpenCarrierAlerts: false, ct);
        if (candidates.Count == 0)
        {
            return promotedIds.Count;
        }

        var delivered = new List<ParcelPollGroup>();
        var returned = 0;
        foreach (var group in PollGroups(candidates.Where(candidate => !promotedIds.Contains(candidate.PurchaseId))))
        {
            var sample = group.Sample;
            if (sample.TrackingProvider is null ||
                !clients.TryGetValue(sample.TrackingProvider, out var client))
            {
                logger.LogWarning(
                    "No tracking provider client is available for purchase {PurchaseId} (provider {Provider}).",
                    sample.PurchaseId, sample.TrackingProvider);
                continue;
            }

            var result = await client.GetTrackingAsync(
                new TrackingLookup(sample.ProviderParcelId, sample.TrackingCode), ct);
            LogCarrierObservation(sample.PurchaseId, result);

            switch (result.Outcome)
            {
                case TrackingOutcome.Delivered:
                    delivered.Add(group);
                    break;

                case TrackingOutcome.Returned:
                    returned += await MarkReturnedAsync(group, client.Provider, ct);
                    break;

                case TrackingOutcome.NotFound:
                    logger.LogWarning(
                        "{Provider} tracking not found for purchase {PurchaseId} (reference {Reference}).",
                        client.Provider, sample.PurchaseId, sample.ProviderParcelId);
                    break;

                case TrackingOutcome.NotConfigured:
                case TrackingOutcome.Failed:
                    logger.LogWarning(
                        "{Provider} tracking check failed for purchase {PurchaseId} (reference {Reference}).",
                        client.Provider, sample.PurchaseId, sample.ProviderParcelId);
                    break;
            }

            if (result.DurableAlert)
            {
                await MarkAlertAsync(group, result.StatusCode!.Value, client.Provider, ct);
            }

            await RememberTrackingUrlAsync(group, result.TrackingUrl, ct);
        }

        var deliveredCount = delivered.Count == 0 ? 0 : await DeliverGroupsAsync(delivered, ct);
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
        foreach (var group in PollGroups(waiting))
        {
            var sample = group.Sample;
            if (sample.TrackingProvider is null ||
                !clients.TryGetValue(sample.TrackingProvider, out var client))
            {
                continue;
            }

            var result = await client.GetTrackingAsync(
                new TrackingLookup(sample.ProviderParcelId, sample.TrackingCode), ct);
            if (LeavesPreparingUnchanged(result))
            {
                logger.LogInformation(
                    "Postex status {StatusCode} with parent {ParentCode} left purchase {PurchaseId} in preparation.",
                    result.StatusCode, result.ParentCode, sample.PurchaseId);
                await RememberTrackingUrlAsync(group, result.TrackingUrl, ct);
                continue;
            }

            if (!PhysicalProductPurchase.IsUsablePostalTrackingCode(result.TrackingCode))
            {
                await RememberTrackingUrlAsync(group, result.TrackingUrl, ct);
                continue;
            }

            var promotedNow = await store.ExecuteLockedAsync(sample.BuyerId, sample.CartId, async () =>
            {
                var targets = await LoadParcelTargetsAsync(group, ct);
                var ids = new List<Guid>();
                var changed = false;
                foreach (var purchase in targets)
                {
                    if (purchase.DeliveryStatus != PhysicalDeliveryStatus.Preparing ||
                        purchase.RemainingQuantity == 0 ||
                        purchase.HasClosedParcelRefund)
                    {
                        continue;
                    }

                    if (purchase.CarrierParcelQuantity != purchase.RemainingQuantity)
                    {
                        purchase.ClearCarrierPaymentPending();
                        purchase.RememberTrackingUrl(result.TrackingUrl);
                        repository.Update(purchase);
                        changed = true;
                        continue;
                    }

                    purchase.MarkShipped(result.TrackingCode, client.Provider, purchase.ProviderParcelId);
                    purchase.RememberTrackingUrl(result.TrackingUrl);
                    repository.Update(purchase);
                    ids.Add(purchase.Id.Value);
                    changed = true;
                }

                if (changed)
                {
                    await unitOfWork.SaveChangesAsync(ct);
                }

                return ids;
            }, ct);

            foreach (var id in promotedNow)
            {
                promoted.Add(id);
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
        ParcelPollGroup group, string provider, CancellationToken ct)
    {
        var sample = group.Sample;
        var changed = await store.ExecuteLockedAsync(sample.BuyerId, sample.CartId, async () =>
        {
            var now = DateTime.UtcNow;
            var count = 0;
            foreach (var purchase in await LoadParcelTargetsAsync(group, ct))
            {
                if (purchase.CarrierReturnedAtUtc.HasValue ||
                    purchase.RemainingQuantity == 0 ||
                    purchase.HasClosedParcelRefund)
                {
                    continue;
                }

                if (purchase.DeliveryStatus != PhysicalDeliveryStatus.Shipped)
                {
                    logger.LogWarning(
                        "{Provider} returned status ignored for purchase {PurchaseId} because delivery status is {Status}.",
                        provider, purchase.Id.Value, purchase.DeliveryStatus);
                    continue;
                }

                purchase.MarkCarrierReturned(now);
                repository.Update(purchase);
                count++;
            }

            if (count > 0)
            {
                await unitOfWork.SaveChangesAsync(ct);
            }

            return count;
        }, ct);

        if (changed > 0)
        {
            logger.LogWarning(
                "{Provider} tracking returned for purchase {PurchaseId} (reference {Reference}); settlement blocked until manual review.",
                provider, sample.PurchaseId, sample.ProviderParcelId);
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
                    purchase.CarrierReturnedAtUtc.HasValue ||
                    purchase.ExcludedFromAutomaticDeliveryByAlert ||
                    purchase.HasClosedParcelRefund)
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

    private async Task<int> DeliverGroupsAsync(
        IReadOnlyList<ParcelPollGroup> groups, CancellationToken ct)
    {
        var delivered = 0;
        foreach (var group in groups)
        {
            var sample = group.Sample;
            delivered += await store.ExecuteLockedAsync(sample.BuyerId, sample.CartId, async () =>
            {
                var nowCount = 0;
                foreach (var purchase in await LoadParcelTargetsAsync(group, ct))
                {
                    if (purchase.DeliveryStatus != PhysicalDeliveryStatus.Shipped ||
                        purchase.RemainingQuantity == 0 ||
                        purchase.CarrierReturnedAtUtc.HasValue ||
                        purchase.ExcludedFromAutomaticDeliveryByAlert ||
                        purchase.HasClosedParcelRefund)
                    {
                        continue;
                    }

                    purchase.MarkDelivered();
                    repository.Update(purchase);
                    nowCount++;
                }

                if (nowCount > 0)
                {
                    await unitOfWork.SaveChangesAsync(ct);
                }

                return nowCount;
            }, ct);
        }

        if (delivered > 0)
        {
            logger.LogInformation(
                "Physical delivery marked Delivered by {Source} for {Count} item(s).", "provider", delivered);
        }

        return delivered;
    }

    private static bool LeavesPreparingUnchanged(TrackingResult result) =>
        result.DurableAlert || result.ParentCode == PhysicalProductPurchase.OthersParentCode;

    private void LogCarrierObservation(Guid purchaseId, TrackingResult result)
    {
        if (result.ParentMismatch)
        {
            logger.LogWarning(
                "Postex status {StatusCode} for purchase {PurchaseId} arrived with parent {ParentCode} instead of parent 8; outcome follows the parent.",
                result.StatusCode, purchaseId, result.ParentCode);
        }

        if (result.ParentCode == PhysicalProductPurchase.OthersParentCode && !result.DurableAlert)
        {
            logger.LogInformation(
                "Postex parent 10 status {StatusCode} left purchase {PurchaseId} unchanged.",
                result.StatusCode, purchaseId);
        }
    }

    private async Task MarkAlertAsync(
        ParcelPollGroup group, int statusCode, string provider, CancellationToken ct)
    {
        var sample = group.Sample;
        var changed = await store.ExecuteLockedAsync(sample.BuyerId, sample.CartId, async () =>
        {
            var now = DateTime.UtcNow;
            var count = 0;
            foreach (var purchase in await LoadParcelTargetsAsync(group, ct))
            {
                if (purchase.CarrierAlertCode.HasValue ||
                    purchase.RemainingQuantity == 0 ||
                    purchase.HasClosedParcelRefund)
                {
                    continue;
                }

                if (purchase.DeliveryStatus != PhysicalDeliveryStatus.Shipped)
                {
                    logger.LogWarning(
                        "{Provider} alert {StatusCode} ignored for purchase {PurchaseId} because delivery status is {Status}.",
                        provider, statusCode, purchase.Id.Value, purchase.DeliveryStatus);
                    continue;
                }

                purchase.MarkCarrierAlert(statusCode, now);
                repository.Update(purchase);
                count++;
            }

            if (count > 0)
            {
                await unitOfWork.SaveChangesAsync(ct);
            }

            return count;
        }, ct);

        if (changed > 0)
        {
            logger.LogWarning(
                "{Provider} recorded carrier alert {StatusCode} for purchase {PurchaseId}; settlement stays blocked until manual review.",
                provider, statusCode, sample.PurchaseId);
        }
    }

    private async Task RememberTrackingUrlAsync(
        ParcelPollGroup group, string? trackingUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(trackingUrl))
        {
            return;
        }

        var sample = group.Sample;
        if (group.Candidates.All(candidate =>
                string.Equals(candidate.TrackingUrl, trackingUrl, StringComparison.Ordinal)))
        {
            return;
        }

        await store.ExecuteLockedAsync(sample.BuyerId, sample.CartId, async () =>
        {
            var changed = false;
            foreach (var purchase in await LoadParcelTargetsAsync(group, ct))
            {
                if (!purchase.RememberTrackingUrl(trackingUrl))
                {
                    continue;
                }

                repository.Update(purchase);
                changed = true;
            }

            if (changed)
            {
                await unitOfWork.SaveChangesAsync(ct);
            }

            return changed;
        }, ct);
    }

    /// <summary>
    /// اقلام هم‌سبد با یک <c>ProviderParcelId</c> یک گروه می‌شوند تا پستکس یک‌بار پرسیده شود.
    /// قلم بدون شناسهٔ مرسوله گروه خودش را دارد.
    /// </summary>
    private static IEnumerable<ParcelPollGroup> PollGroups(IEnumerable<PhysicalDeliveryCandidate> candidates)
    {
        return candidates
            .GroupBy(candidate => (
                candidate.CartId,
                Provider: candidate.TrackingProvider ?? string.Empty,
                Parcel: string.IsNullOrWhiteSpace(candidate.ProviderParcelId)
                    ? $"purchase:{candidate.PurchaseId:N}"
                    : candidate.ProviderParcelId))
            .Select(group => new ParcelPollGroup(group.ToList()));
    }

    /// <summary>
    /// اگر فروشگاه اقلام سبد را برگرداند، همهٔ هم‌مرسوله‌ها با هم به‌روز می‌شوند.
    /// در غیر این صورت فقط قلم‌های همین گروه از مخزن خوانده می‌شوند.
    /// </summary>
    private async Task<List<PhysicalProductPurchase>> LoadParcelTargetsAsync(
        ParcelPollGroup group, CancellationToken ct)
    {
        var sample = group.Sample;
        if (!string.IsNullOrWhiteSpace(sample.ProviderParcelId))
        {
            var mates = (await store.GetPurchasesAsync(sample.CartId, ct))
                .Where(purchase => string.Equals(
                    purchase.ProviderParcelId, sample.ProviderParcelId, StringComparison.Ordinal))
                .ToList();
            if (mates.Count > 0)
            {
                return mates;
            }
        }

        var loaded = new List<PhysicalProductPurchase>();
        foreach (var candidate in group.Candidates)
        {
            var purchase = await repository.GetByIdAsync(
                new PhysicalProductPurchaseId(candidate.PurchaseId), ct);
            if (purchase is not null)
            {
                loaded.Add(purchase);
            }
        }

        return loaded;
    }

    private sealed class ParcelPollGroup(IReadOnlyList<PhysicalDeliveryCandidate> candidates)
    {
        public IReadOnlyList<PhysicalDeliveryCandidate> Candidates { get; } = candidates;

        public PhysicalDeliveryCandidate Sample => Candidates[0];
    }
}
