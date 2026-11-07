using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ByeMoney.Infrastructure.Modules.Purchases.Persistence;

public sealed class OrderSettlementStore(ApplicationDbContext db) : IOrderSettlementStore
{
    public async Task<IReadOnlySet<string>> GetSettledOrderIdsAsync(CancellationToken ct)
    {
        var orderIds = await db.Set<OrderSettlementItem>()
            .AsNoTracking()
            .Select(x => x.OrderId)
            .ToListAsync(ct);

        return orderIds.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyDictionary<Guid, PaymentShares>> GetShippingPaymentsAsync(
        IReadOnlyCollection<Guid> cartIds, CancellationToken ct)
    {
        if (cartIds.Count == 0)
        {
            return new Dictionary<Guid, PaymentShares>();
        }

        // تراکنش پرداخت سبد همان شناسهٔ سبد است؛ بدهی ارسال مرجع خود سبد دارد و بدهی اقلام مرجع شناسهٔ خرید (D08).
        var debits = await db.Set<LedgerEntry>()
            .AsNoTracking()
            .Where(x => cartIds.Contains(x.TransactionId) &&
                x.ReferenceType == LedgerReferenceType.Purchase &&
                x.Amount < 0)
            .Select(x => new { x.TransactionId, x.ReferenceId, x.BalanceType, x.Amount })
            .ToListAsync(ct);

        return debits
            .Where(x => x.ReferenceId == x.TransactionId.ToString())
            .GroupBy(x => x.TransactionId)
            .ToDictionary(
                group => group.Key,
                group => new PaymentShares(
                    -group.Where(x => x.BalanceType == LedgerBalanceType.Regular).Sum(x => x.Amount),
                    -group.Where(x => x.BalanceType == LedgerBalanceType.Gift).Sum(x => x.Amount)));
    }

    public async Task<bool> TryAddAsync(OrderSettlementPeriod period, CancellationToken ct)
    {
        db.Add(period);

        try
        {
            // SaveChanges دوره و همهٔ سفارش‌ها را در یک تراکنش ذخیره می‌کند؛ نقض کلید یعنی هیچ ردیفی ثبت نشده است.
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: OrderSettlementItemConfiguration.PrimaryKeyName
        })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public Task<OrderSettlementPeriodResponse?> FindPeriodAsync(Guid periodId, CancellationToken ct)
    {
        var id = new OrderSettlementPeriodId(periodId);
        var periods = db.Set<OrderSettlementPeriod>()
            .AsNoTracking()
            .Where(x => x.Id == id);

        return Project(periods).SingleOrDefaultAsync(ct);
    }

    public async Task<PurchasePage<OrderSettlementPeriodResponse>> GetPeriodsAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var totalCount = await db.Set<OrderSettlementPeriod>().CountAsync(ct);
        var items = await (
                from period in db.Set<OrderSettlementPeriod>().AsNoTracking()
                join user in db.Set<User>().AsNoTracking() on period.ClosedByUserId equals user.Id
                orderby period.CreatedAtUtc descending, period.Id descending
                select new OrderSettlementPeriodResponse(
                    period.Id.Value,
                    period.Title,
                    period.Note,
                    period.OrderCount,
                    period.TotalRegularNetAmountInNoor,
                    period.TotalGiftNetAmountInNoor,
                    period.TotalNetAmountInNoor,
                    user.ExternalUserId,
                    period.CreatedAtUtc))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PurchasePage<OrderSettlementPeriodResponse>(items, page, pageSize, totalCount);
    }

    public Task<bool> PeriodExistsAsync(Guid periodId, CancellationToken ct)
    {
        var id = new OrderSettlementPeriodId(periodId);
        return db.Set<OrderSettlementPeriod>().AnyAsync(x => x.Id == id, ct);
    }

    public async Task<PurchasePage<SettlementOrderResponse>> GetPeriodOrdersAsync(
        Guid periodId, int page, int pageSize, CancellationToken ct)
    {
        var id = new OrderSettlementPeriodId(periodId);
        var orders = db.Set<OrderSettlementItem>()
            .AsNoTracking()
            .Where(x => x.PeriodId == id);

        var totalCount = await orders.CountAsync(ct);
        var items = await orders
            .OrderBy(x => x.OrderCreatedAtUtc)
            .ThenBy(x => x.OrderId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SettlementOrderResponse(
                x.OrderId,
                x.SourceType,
                x.BuyerExternalUserId,
                x.OrderCreatedAtUtc,
                x.RegularNetAmountInNoor,
                x.GiftNetAmountInNoor,
                x.NetAmountInNoor))
            .ToListAsync(ct);

        return new PurchasePage<SettlementOrderResponse>(items, page, pageSize, totalCount);
    }

    private IQueryable<OrderSettlementPeriodResponse> Project(IQueryable<OrderSettlementPeriod> periods) =>
        from period in periods
        join user in db.Set<User>().AsNoTracking() on period.ClosedByUserId equals user.Id
        select new OrderSettlementPeriodResponse(
            period.Id.Value,
            period.Title,
            period.Note,
            period.OrderCount,
            period.TotalRegularNetAmountInNoor,
            period.TotalGiftNetAmountInNoor,
            period.TotalNetAmountInNoor,
            user.ExternalUserId,
            period.CreatedAtUtc);
}
