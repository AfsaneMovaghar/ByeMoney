using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public sealed class PhysicalDeliveryStore(ApplicationDbContext db) : IPhysicalDeliveryStore
{
    public Task<PhysicalPurchaseOwner?> FindOwnerAsync(Guid purchaseId, CancellationToken ct) =>
        db.Set<PhysicalProductPurchase>()
            .AsNoTracking()
            .Where(x => x.Id == new PhysicalProductPurchaseId(purchaseId))
            .Select(x => new PhysicalPurchaseOwner(x.BuyerId.Value, x.CartId))
            .SingleOrDefaultAsync(ct);

    public Task<PhysicalPurchaseOwner?> FindCartOwnerAsync(Guid cartId, CancellationToken ct) =>
        db.Set<Cart>()
            .AsNoTracking()
            .Where(x => x.Id == cartId)
            .Select(x => new PhysicalPurchaseOwner(x.BuyerId.Value, x.Id))
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<PhysicalProductPurchase>> GetPurchasesAsync(Guid cartId, CancellationToken ct) =>
        await db.Set<PhysicalProductPurchase>()
            .Where(x => x.CartId == cartId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

    public async Task<ShippingAddress?> GetCartShippingAddressAsync(Guid cartId, CancellationToken ct)
    {
        var cart = await db.Set<Cart>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == cartId, ct);
        return cart?.ShippingAddress;
    }

    public async Task<string?> GetCartShippingOriginIdAsync(Guid cartId, CancellationToken ct)
    {
        var cart = await db.Set<Cart>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == cartId, ct);
        return cart?.ShippingOriginId;
    }

    public async Task<IReadOnlyList<PhysicalDeliveryCandidate>> GetShippedAsync(
        DateTime shippedAtOrBeforeUtc, IReadOnlyCollection<string> providers, int limit, CancellationToken ct)
    {
        var providerKeys = providers.ToArray();
        var query = db.Set<PhysicalProductPurchase>()
            .AsNoTracking()
            .Where(x => x.DeliveryStatus == PhysicalDeliveryStatus.Shipped &&
                x.CarrierReturnedAtUtc == null &&
                x.ShippedAtUtc != null && x.ShippedAtUtc <= shippedAtOrBeforeUtc);

        if (providerKeys.Length > 0)
        {
            query = query.Where(x => x.TrackingProvider != null && providerKeys.Contains(x.TrackingProvider));
        }

        return await query
            .OrderBy(x => x.ShippedAtUtc)
            .Take(limit)
            .Select(x => new PhysicalDeliveryCandidate(
                x.Id.Value, x.BuyerId.Value, x.CartId,
                x.TrackingCode, x.TrackingProvider, x.ProviderParcelId, x.ShippedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PhysicalDeliveryCandidate>> GetPreparingWithCarrierReferenceAsync(
        IReadOnlyCollection<string> providers, int limit, CancellationToken ct)
    {
        var providerKeys = providers.ToArray();
        return await db.Set<PhysicalProductPurchase>()
            .AsNoTracking()
            .Where(x => x.DeliveryStatus == PhysicalDeliveryStatus.Preparing &&
                x.ProviderParcelId != null &&
                x.TrackingProvider != null &&
                x.CancelledQuantity < x.Quantity &&
                providerKeys.Contains(x.TrackingProvider))
            .OrderBy(x => x.CreatedAtUtc)
            .Take(limit)
            .Select(x => new PhysicalDeliveryCandidate(
                x.Id.Value, x.BuyerId.Value, x.CartId,
                x.TrackingCode, x.TrackingProvider, x.ProviderParcelId, x.ShippedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<T> ExecuteLockedAsync<T>(Guid buyerId, Guid cartId, Func<Task<T>> work, CancellationToken ct)
    {
        // ترتیب قفل‌ها (خریدار، سپس سبد) همان ترتیب لغو است؛ استثنای کار بدون commit تراکنش را برمی‌گرداند.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await PurchaseAdvisoryLocks.LockBuyerAsync(db, buyerId, ct);
        await PurchaseAdvisoryLocks.LockCartAsync(db, cartId, ct);

        var result = await work();
        await tx.CommitAsync(ct);
        return result;
    }
}
