using ByeMoney.Application.Modules.Purchases.History.Constants;
using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.History.Interfaces;
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

    private IQueryable<PhysicalProductPurchase> PaidPhysicalPurchases => db.Set<PhysicalProductPurchase>().AsNoTracking()
        .Where(purchase => purchase.LedgerTransactionId != null);

    public async Task<PurchasePage<PurchaseReadRow>> ListAsync(
        Guid? buyerId, PurchaseHistoryFilter filter, CancellationToken ct)
    {
        var courseQuery = from purchase in PaidPurchases
                          join buyer in db.Set<User>().AsNoTracking() on purchase.BuyerId equals buyer.Id
                          select new { Purchase = purchase, Buyer = buyer };

        var physicalQuery = from purchase in PaidPhysicalPurchases
                            join buyer in db.Set<User>().AsNoTracking() on purchase.BuyerId equals buyer.Id
                            select new { Purchase = purchase, Buyer = buyer };

        if (buyerId.HasValue)
        {
            var owner = new UserId(buyerId.Value);
            courseQuery = courseQuery.Where(row => row.Purchase.BuyerId == owner);
            physicalQuery = physicalQuery.Where(row => row.Purchase.BuyerId == owner);
        }

        if (filter.BuyerExternalUserId is not null)
        {
            courseQuery = courseQuery.Where(row => row.Buyer.ExternalUserId == filter.BuyerExternalUserId);
            physicalQuery = physicalQuery.Where(row => row.Buyer.ExternalUserId == filter.BuyerExternalUserId);
        }

        if (filter.FromUtc.HasValue)
        {
            var start = filter.FromUtc.Value.UtcDateTime;
            courseQuery = courseQuery.Where(row => row.Purchase.Snapshot.PurchasedAt >= start);
            physicalQuery = physicalQuery.Where(row => row.Purchase.CreatedAtUtc >= start);
        }

        if (filter.ToUtc.HasValue)
        {
            var end = filter.ToUtc.Value.UtcDateTime;
            courseQuery = courseQuery.Where(row => row.Purchase.Snapshot.PurchasedAt < end);
            physicalQuery = physicalQuery.Where(row => row.Purchase.CreatedAtUtc < end);
        }

        var includePhysical = true;
        if (filter.DeliveryStatus == PurchaseDeliveryStatus.Delivered.ToString())
        {
            courseQuery = courseQuery.Where(row => row.Purchase.Status == CoursePurchaseStatus.NotificationSent);
            includePhysical = false;
        }
        else if (filter.DeliveryStatus == PurchaseDeliveryStatus.Pending.ToString())
        {
            courseQuery = courseQuery.Where(row => row.Purchase.Status != CoursePurchaseStatus.NotificationSent);
        }

        var courseCount = await courseQuery.CountAsync(ct);
        var physicalCount = includePhysical ? await physicalQuery.CountAsync(ct) : 0;
        var totalCount = courseCount + physicalCount;

        var takeLimit = filter.Page * filter.PageSize;

        var courseItems = await courseQuery
            .OrderByDescending(row => row.Purchase.Snapshot.PurchasedAt)
            .ThenByDescending(row => row.Purchase.Id)
            .Take(takeLimit)
            .Select(row => new PurchaseReadRow(
                row.Purchase.Id.Value,
                row.Purchase.CartId,
                row.Purchase.Snapshot.ProductTitle,
                row.Purchase.Snapshot.PriceInNoorAtPurchaseTime,
                row.Purchase.Snapshot.PurchasedAt,
                row.Purchase.Status,
                row.Buyer.ExternalUserId,
                row.Buyer.FirstName,
                row.Buyer.LastName,
                row.Purchase.Snapshot.ItemType,
                row.Purchase.Snapshot.ExternalParentId))
            .ToListAsync(ct);

        List<PurchaseReadRow> physicalItems = [];
        if (includePhysical && physicalCount > 0)
        {
            physicalItems = await physicalQuery
                .OrderByDescending(row => row.Purchase.CreatedAtUtc)
                .ThenByDescending(row => row.Purchase.Id)
                .Take(takeLimit)
                .Select(row => new PurchaseReadRow(
                    row.Purchase.Id.Value,
                    row.Purchase.CartId,
                    row.Purchase.ProductTitle,
                    row.Purchase.PaidAmountInNoor,
                    row.Purchase.CreatedAtUtc,
                    CoursePurchaseStatus.Debited,
                    row.Buyer.ExternalUserId,
                    row.Buyer.FirstName,
                    row.Buyer.LastName,
                    ProductItemType.Product,
                    null))
                .ToListAsync(ct);
        }

        var pagedItems = courseItems
            .Concat(physicalItems)
            .OrderByDescending(row => row.PurchasedAtUtc)
            .ThenByDescending(row => row.PurchaseId)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToArray();

        return new PurchasePage<PurchaseReadRow>(pagedItems, filter.Page, filter.PageSize, totalCount);
    }

    public Task<PurchaseReceiptData?> GetReceiptAsync(Guid purchaseId, Guid buyerId, CancellationToken ct)
    {
        return ReadReceiptAsync(purchaseId, buyerId, ct);
    }

    public Task<PurchaseReceiptData?> GetAdminReceiptAsync(Guid purchaseId, CancellationToken ct)
    {
        return ReadReceiptAsync(purchaseId, null, ct);
    }

    private async Task<PurchaseReceiptData?> ReadReceiptAsync(Guid purchaseId, Guid? buyerId, CancellationToken ct)
    {
        var id = new CoursePurchaseId(purchaseId);
        var query = PaidPurchases.Where(item => item.Id == id);
        if (buyerId.HasValue)
        {
            var owner = new UserId(buyerId.Value);
            query = query.Where(item => item.BuyerId == owner);
        }

        var purchase = await query.SingleOrDefaultAsync(ct);
        if (purchase is not null)
        {
            var cart = purchase.CartId.HasValue
                ? await db.Set<CourseCart>().AsNoTracking().SingleOrDefaultAsync(
                    item => item.Id == purchase.CartId && item.BuyerId == purchase.BuyerId.Value, ct)
                : null;
            return new PurchaseReceiptData(purchase, cart);
        }

        var physicalId = new PhysicalProductPurchaseId(purchaseId);
        var physicalQuery = PaidPhysicalPurchases.Where(item => item.Id == physicalId);
        if (buyerId.HasValue)
        {
            var owner = new UserId(buyerId.Value);
            physicalQuery = physicalQuery.Where(item => item.BuyerId == owner);
        }

        var physical = await physicalQuery.SingleOrDefaultAsync(ct);
        if (physical is not null)
        {
            var cart = await db.Set<CourseCart>().AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == physical.CartId && item.BuyerId == physical.BuyerId.Value, ct);
            return new PurchaseReceiptData(null, cart, physical);
        }

        return null;
    }
}
