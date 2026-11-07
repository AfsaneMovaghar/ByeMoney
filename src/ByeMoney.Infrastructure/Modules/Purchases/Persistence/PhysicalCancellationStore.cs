using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Constants;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
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

public sealed class PhysicalCancellationStore(ApplicationDbContext db) : IPhysicalCancellationStore
{
    public Task<PhysicalPurchaseOwner?> FindOwnerAsync(Guid purchaseId, CancellationToken ct) =>
        db.Set<PhysicalProductPurchase>()
            .AsNoTracking()
            .Where(x => x.Id == new PhysicalProductPurchaseId(purchaseId))
            .Select(x => new PhysicalPurchaseOwner(x.BuyerId.Value, x.CartId))
            .SingleOrDefaultAsync(ct);

    public Task<Cart?> FindCartAsync(Guid cartId, CancellationToken ct) =>
        db.Set<Cart>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == cartId, ct);

    public async Task<IReadOnlyList<PhysicalProductPurchase>> GetPurchasesAsync(Guid cartId, CancellationToken ct) =>
        await db.Set<PhysicalProductPurchase>()
            .Where(x => x.CartId == cartId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PhysicalProductCancellation>> GetOperationAsync(Guid operationId, CancellationToken ct) =>
        await db.Set<PhysicalProductCancellation>()
            .AsNoTracking()
            .Where(x => x.OperationId == operationId)
            .OrderBy(x => x.PurchaseId)
            .ToListAsync(ct);

    public async Task<PaymentShares> GetShippingPaymentAsync(Guid cartId, CancellationToken ct)
    {
        // ارسال ستون سهم ندارد؛ بدهی خریدار با مرجع خود سبد در تراکنش پرداخت همان سبد است (D08).
        var referenceId = cartId.ToString();
        var debits = await db.Set<LedgerEntry>()
            .AsNoTracking()
            .Where(x => x.TransactionId == cartId &&
                x.ReferenceType == LedgerReferenceType.Purchase &&
                x.ReferenceId == referenceId &&
                x.Amount < 0)
            .GroupBy(x => x.BalanceType)
            .Select(g => new { BalanceType = g.Key, Amount = -g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        var regular = debits.Where(x => x.BalanceType == LedgerBalanceType.Regular).Sum(x => x.Amount);
        var gift = debits.Where(x => x.BalanceType == LedgerBalanceType.Gift).Sum(x => x.Amount);
        return new PaymentShares(regular, gift);
    }

    public void Add(PhysicalProductCancellation cancellation) => db.Add(cancellation);

    public async Task<PhysicalRestockRequest?> FindPendingRestockAsync(Guid operationId, CancellationToken ct)
    {
        // commit سبد در استرپی با شناسهٔ رزرو انجام شده است؛ برگشت موجودی هم با همان شناسه فرستاده می‌شود.
        var rows = await (
                from cancellation in db.Set<PhysicalProductCancellation>()
                join cart in db.Set<Cart>() on cancellation.CartId equals cart.Id
                where cancellation.OperationId == operationId && cancellation.StockReturnedAtUtc == null
                orderby cancellation.PurchaseId
                select new { cart.ReservationId, cancellation.PurchaseId, cancellation.Quantity })
            .AsNoTracking()
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            return null;
        }

        var items = rows
            .Select(x => new PhysicalRestockItem(x.PurchaseId.Value, x.Quantity))
            .ToArray();
        return new PhysicalRestockRequest(rows[0].ReservationId, operationId, items);
    }

    public async Task<IReadOnlyList<Guid>> GetPendingRestockOperationsAsync(
        int limit, int maxAttempts, CancellationToken ct) =>
        await db.Set<PhysicalProductCancellation>()
            .AsNoTracking()
            .Where(x => x.StockReturnedAtUtc == null && x.StockReturnAttempts < maxAttempts)
            .GroupBy(x => x.OperationId)
            .Select(g => new { OperationId = g.Key, CreatedAtUtc = g.Min(x => x.CreatedAtUtc) })
            .OrderBy(x => x.CreatedAtUtc)
            .Take(limit)
            .Select(x => x.OperationId)
            .ToListAsync(ct);

    public Task MarkStockReturnedAsync(Guid operationId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return db.Set<PhysicalProductCancellation>()
            .Where(x => x.OperationId == operationId && x.StockReturnedAtUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.StockReturnedAtUtc, now)
                .SetProperty(x => x.StockReturnErrorCode, (string?)null)
                .SetProperty(x => x.UpdatedAt, now), ct);
    }

    public Task RecordStockReturnFailureAsync(Guid operationId, string? errorCode, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        // قطعی موقت استرپی (شبکه یا 5xx) از سقف تلاش کم نمی‌کند؛ فقط خطای دائمی شمرده می‌شود.
        var increment = errorCode == CartErrorCodes.Unavailable ? 0 : 1;
        return db.Set<PhysicalProductCancellation>()
            .Where(x => x.OperationId == operationId && x.StockReturnedAtUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.StockReturnErrorCode, errorCode)
                .SetProperty(x => x.StockReturnAttempts, x => x.StockReturnAttempts + increment)
                .SetProperty(x => x.UpdatedAt, now), ct);
    }

    public Task RecordStockReturnErrorAsync(Guid operationId, string? errorCode, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return db.Set<PhysicalProductCancellation>()
            .Where(x => x.OperationId == operationId && x.StockReturnedAtUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.StockReturnErrorCode, errorCode)
                .SetProperty(x => x.UpdatedAt, now), ct);
    }

    public Task<int> MarkStockResolvedManuallyAsync(Guid operationId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return db.Set<PhysicalProductCancellation>()
            .Where(x => x.OperationId == operationId && x.StockReturnedAtUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.StockReturnedAtUtc, now)
                .SetProperty(x => x.StockReturnErrorCode, PhysicalCancellationErrorCodes.RestockResolvedManually)
                .SetProperty(x => x.UpdatedAt, now), ct);
    }

    public async Task<IReadOnlyList<PendingRestockRow>> GetUnfinishedRestocksAsync(int limit, CancellationToken ct) =>
        await db.Set<PhysicalProductCancellation>()
            .AsNoTracking()
            .Where(x => x.StockReturnedAtUtc == null)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.PurchaseId)
            .Take(limit)
            .Select(x => new PendingRestockRow(
                x.OperationId,
                x.CartId,
                x.PurchaseId.Value,
                x.Quantity,
                x.StockReturnAttempts,
                x.StockReturnErrorCode,
                x.CreatedAtUtc,
                x.UpdatedAt))
            .ToListAsync(ct);

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
        return Result<T>.Conflict(PhysicalCancellationErrors.Conflict, PhysicalCancellationErrorCodes.Conflict);
    }
}
