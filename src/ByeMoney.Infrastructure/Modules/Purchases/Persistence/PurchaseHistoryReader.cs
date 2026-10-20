using ByeMoney.Application.Modules.Purchases.Queries.History;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public sealed class PurchaseHistoryReader(ApplicationDbContext db) : IPurchaseHistoryReader
{
    private IQueryable<CoursePurchase> PaidPurchases => db.Set<CoursePurchase>().AsNoTracking()
        .Where(purchase => purchase.Status == CoursePurchaseStatus.Debited ||
            purchase.Status == CoursePurchaseStatus.NotificationSent ||
            purchase.Status == CoursePurchaseStatus.NotificationFailed);

    public async Task<PurchasePage<PurchaseReadRow>> ListAsync(
        Guid? buyerId, PurchaseHistoryFilter filter, CancellationToken ct)
    {
        var query = from purchase in PaidPurchases
                    join buyer in db.Set<User>().AsNoTracking() on purchase.BuyerId equals buyer.Id
                    select new { Purchase = purchase, Buyer = buyer };

        if (buyerId.HasValue)
        {
            var owner = new UserId(buyerId.Value);
            query = query.Where(row => row.Purchase.BuyerId == owner);
        }
        if (filter.BuyerExternalUserId is not null)
        {
            query = query.Where(row => row.Buyer.ExternalUserId == filter.BuyerExternalUserId);
        }
        if (filter.FromUtc.HasValue)
        {
            var start = filter.FromUtc.Value.UtcDateTime;
            query = query.Where(row => row.Purchase.Snapshot.PurchasedAt >= start);
        }
        if (filter.ToUtc.HasValue)
        {
            var end = filter.ToUtc.Value.UtcDateTime;
            query = query.Where(row => row.Purchase.Snapshot.PurchasedAt < end);
        }
        if (filter.DeliveryStatus == PurchaseDeliveryStatus.Delivered.ToString())
        {
            query = query.Where(row => row.Purchase.Status == CoursePurchaseStatus.NotificationSent);
        }
        else if (filter.DeliveryStatus == PurchaseDeliveryStatus.Pending.ToString())
        {
            query = query.Where(row => row.Purchase.Status != CoursePurchaseStatus.NotificationSent);
        }

        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(row => row.Purchase.Snapshot.PurchasedAt)
            .ThenByDescending(row => row.Purchase.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(row => new PurchaseReadRow(
                row.Purchase.Id.Value, row.Purchase.CartId, row.Purchase.Snapshot.ProductTitle,
                row.Purchase.Snapshot.PriceInNoorAtPurchaseTime, row.Purchase.Snapshot.PurchasedAt,
                row.Purchase.Status, row.Buyer.ExternalUserId, row.Buyer.FirstName, row.Buyer.LastName))
            .ToListAsync(ct);

        return new PurchasePage<PurchaseReadRow>(items, filter.Page, filter.PageSize, count);
    }

    public async Task<PurchaseReceiptData?> GetReceiptAsync(Guid purchaseId, Guid buyerId, CancellationToken ct)
    {
        var id = new CoursePurchaseId(purchaseId);
        var owner = new UserId(buyerId);
        var purchase = await PaidPurchases.SingleOrDefaultAsync(
            item => item.Id == id && item.BuyerId == owner, ct);
        if (purchase is null)
        {
            return null;
        }

        var cart = purchase.CartId.HasValue
            ? await db.Set<CourseCart>().AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == purchase.CartId && item.BuyerId == buyerId, ct)
            : null;
        return new PurchaseReceiptData(purchase, cart);
    }
}
