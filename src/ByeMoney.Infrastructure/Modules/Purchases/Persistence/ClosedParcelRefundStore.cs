using ByeMoney.Application.Modules.Purchases.Orders.Constants;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Constants;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public sealed class ClosedParcelRefundStore(ApplicationDbContext db) : IClosedParcelRefundStore
{
    public Task<PhysicalPurchaseOwner?> FindOwnerAsync(Guid purchaseId, CancellationToken ct) =>
        db.Set<PhysicalProductPurchase>()
            .AsNoTracking()
            .Where(x => x.Id == new PhysicalProductPurchaseId(purchaseId))
            .Select(x => new PhysicalPurchaseOwner(x.BuyerId.Value, x.CartId))
            .SingleOrDefaultAsync(ct);

    public Task<PhysicalProductPurchase?> FindPurchaseAsync(Guid purchaseId, CancellationToken ct) =>
        db.Set<PhysicalProductPurchase>()
            .SingleOrDefaultAsync(x => x.Id == new PhysicalProductPurchaseId(purchaseId), ct);

    public Task<ClosedParcelRefund?> FindByOperationIdAsync(Guid operationId, CancellationToken ct) =>
        db.Set<ClosedParcelRefund>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperationId == operationId, ct);

    public Task<ClosedParcelRefund?> FindByPurchaseIdAsync(Guid purchaseId, CancellationToken ct) =>
        db.Set<ClosedParcelRefund>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PurchaseId == new PhysicalProductPurchaseId(purchaseId), ct);

    public async Task<bool> IsOperationIdUsedElsewhereAsync(Guid operationId, CancellationToken ct) =>
        await db.Set<PhysicalProductCancellation>().AnyAsync(x => x.OperationId == operationId, ct) ||
        await db.Set<PhysicalReservationCancellation>().AnyAsync(x => x.OperationId == operationId, ct) ||
        await db.Set<LedgerEntry>().AnyAsync(x => x.TransactionId == operationId, ct);

    public Task<bool> IsOrderSettledAsync(Guid cartId, Guid purchaseId, CancellationToken ct)
    {
        var cartOrderId = OrderIdFormat.For(OrderSourceType.Cart, cartId);
        var purchaseOrderId = OrderIdFormat.For(OrderSourceType.Purchase, purchaseId);
        return db.Set<OrderSettlementItem>()
            .AnyAsync(x => x.OrderId == cartOrderId || x.OrderId == purchaseOrderId, ct);
    }

    public void Add(ClosedParcelRefund refund) => db.Add(refund);

    public async Task<Result<T>> ExecuteAsync<T>(
        Guid buyerId, Guid cartId, Func<Task<Result<T>>> work, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await PurchaseAdvisoryLocks.LockBuyerAsync(db, buyerId, ct);
            await PurchaseAdvisoryLocks.LockCartAsync(db, cartId, ct);

            var result = await work();
            if (result.IsFailure)
            {
                db.ChangeTracker.Clear();
                return result;
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict<T>();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.CheckViolation
        })
        {
            return Conflict<T>();
        }
        catch (DomainException)
        {
            return Conflict<T>();
        }
    }

    private Result<T> Conflict<T>()
    {
        db.ChangeTracker.Clear();
        return Result<T>.Conflict(
            PhysicalCancellationErrors.ClosedParcelRefundConflict,
            ClosedParcelRefundErrorCodes.Conflict);
    }
}
