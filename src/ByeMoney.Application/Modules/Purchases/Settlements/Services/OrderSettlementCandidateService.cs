using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Modules.Purchases.Settlements.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.Interfaces;

namespace ByeMoney.Application.Modules.Purchases.Settlements.Services;

/// <summary>سفارش‌های قابل تسویه‌ای که هنوز در هیچ دوره‌ای ثبت نشده‌اند، به ترتیب زمان ساخت سفارش.</summary>
public sealed class OrderSettlementCandidateService(ICourseOrderReader reader, IOrderSettlementStore store)
{
    public async Task<IReadOnlyList<SettlementOrderResponse>> GetUnsettledAsync(CancellationToken ct)
    {
        var snapshot = await reader.ReadAsync(null, null, ct);
        var settledOrderIds = await store.GetSettledOrderIdsAsync(ct);
        var buyers = snapshot.Buyers.ToDictionary(buyer => buyer.Id);
        var refundsByCart = CourseOrderRefunds.ByCart(snapshot);

        return CourseOrderMapping.Build(snapshot)
            .Where(row => !settledOrderIds.Contains(row.Details.OrderId))
            .Where(row => CourseOrderSettlementRule.IsSettleable(row.Details))
            .Select(row => new SettlementOrderResponse(
                row.Details.OrderId,
                row.Details.SourceType,
                buyers[row.BuyerId].ExternalUserId,
                row.Details.CreatedAtUtc,
                CourseOrderSettlementRule.NetAmount(
                    row.Details,
                    CourseOrderRefunds.For(row.Details, refundsByCart))))
            .OrderBy(order => order.OrderCreatedAtUtc)
            .ThenBy(order => order.OrderId, StringComparer.Ordinal)
            .ToArray();
    }
}
