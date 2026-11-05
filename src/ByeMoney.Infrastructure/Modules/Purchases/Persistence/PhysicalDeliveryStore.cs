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
