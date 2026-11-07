using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.DTOs;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Settlements.Interfaces;

public interface IOrderSettlementStore
{
    Task<IReadOnlySet<string>> GetSettledOrderIdsAsync(CancellationToken ct);

    /// <summary>
    /// دوره و سفارش‌هایش را اتمیک ذخیره می‌کند. اگر سفارشی هم‌زمان در دورهٔ دیگری ثبت شده باشد،
    /// قید یکتایی دیتابیس مانع می‌شود، هیچ ردیفی ذخیره نمی‌شود و <c>false</c> برمی‌گردد.
    /// </summary>
    Task<bool> TryAddAsync(OrderSettlementPeriod period, CancellationToken ct);

    Task<OrderSettlementPeriodResponse?> FindPeriodAsync(Guid periodId, CancellationToken ct);

    Task<PurchasePage<OrderSettlementPeriodResponse>> GetPeriodsAsync(int page, int pageSize, CancellationToken ct);

    Task<bool> PeriodExistsAsync(Guid periodId, CancellationToken ct);

    Task<PurchasePage<SettlementOrderResponse>> GetPeriodOrdersAsync(
        Guid periodId, int page, int pageSize, CancellationToken ct);
}
