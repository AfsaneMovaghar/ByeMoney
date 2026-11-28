using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using System.Data;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public sealed class OrderReader(ApplicationDbContext db) : IOrderReader
{
    public async Task<OrderSnapshot> ReadAsync(
        Guid? buyerId, string? buyerExternalUserId, string scopeId, CancellationToken ct)
    {
        // تصویر ثابت مانع مشاهدهٔ نیمهٔ قبل و بعد از commit هم‌زمان خرید می‌شود.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var users = db.Set<User>().AsNoTracking();
        if (buyerId.HasValue)
        {
            var id = new UserId(buyerId.Value);
            users = users.Where(user => user.Id == id);
        }
        if (buyerExternalUserId is not null)
        {
            users = users.Where(user => user.ExternalUserId == buyerExternalUserId);
        }

        var buyers = await users.Select(user => new OrderBuyer(
            user.Id.Value, user.ExternalUserId, (user.FirstName + " " + user.LastName).Trim())).ToListAsync(ct);
        var userIds = users.Select(user => user.Id);
        var cartOwners = buyers.Select(buyer => new UserId(buyer.Id)).ToArray();
        var carts = await db.Set<Cart>().AsNoTracking()
            .Where(cart => cartOwners.Contains(cart.BuyerId) && cart.ScopeId == scopeId).AsSplitQuery().ToListAsync(ct);
        var topUps = await db.Set<TopUpRequest>().AsNoTracking()
            .Where(topUp => userIds.Contains(topUp.UserId) && topUp.ScopeId == scopeId).ToListAsync(ct);
        var purchases = await db.Set<CoursePurchase>().AsNoTracking()
            .Where(purchase => userIds.Contains(purchase.BuyerId) && purchase.ScopeId == scopeId).ToListAsync(ct);
        var physicalPurchases = await db.Set<PhysicalProductPurchase>().AsNoTracking()
            .Where(purchase => userIds.Contains(purchase.BuyerId) && purchase.ScopeId == scopeId).ToListAsync(ct);
        var physicalPurchaseIds = db.Set<PhysicalProductPurchase>()
            .Where(purchase => userIds.Contains(purchase.BuyerId) && purchase.ScopeId == scopeId)
            .Select(purchase => purchase.Id);
        var cancellations = await db.Set<PhysicalProductCancellation>().AsNoTracking()
            .Where(cancellation => physicalPurchaseIds.Contains(cancellation.PurchaseId)).ToListAsync(ct);
        var closedParcelRefunds = await db.Set<ClosedParcelRefund>().AsNoTracking()
            .Where(refund => physicalPurchaseIds.Contains(refund.PurchaseId)).ToListAsync(ct);
        var settledOrderIds = await db.Set<OrderSettlementItem>().AsNoTracking()
            .Join(
                db.Set<OrderSettlementPeriod>().AsNoTracking().Where(period => period.ScopeId == scopeId),
                item => item.PeriodId,
                period => period.Id,
                (item, _) => item.OrderId)
            .ToListAsync(ct);
        await transaction.CommitAsync(ct);
        return new OrderSnapshot(
            carts, topUps, purchases, buyers, physicalPurchases, cancellations,
            closedParcelRefunds, settledOrderIds.ToHashSet(StringComparer.Ordinal));
    }
}
