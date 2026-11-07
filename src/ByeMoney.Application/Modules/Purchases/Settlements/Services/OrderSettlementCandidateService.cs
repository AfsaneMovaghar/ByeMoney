using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
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
        var settleable = CourseOrderMapping.Build(snapshot)
            .Where(row => !settledOrderIds.Contains(row.Details.OrderId))
            .Where(row => CourseOrderSettlementRule.IsSettleable(row.Details))
            .ToArray();

        var cartIds = settleable
            .Where(row => row.Details.CartId.HasValue)
            .Select(row => row.Details.CartId!.Value)
            .ToArray();
        var shippingByCart = await store.GetShippingPaymentsAsync(cartIds, ct);
        var payments = OrderSettlementShares.BuildPayments(snapshot, shippingByCart);
        var buyers = snapshot.Buyers.ToDictionary(buyer => buyer.Id);
        var refundsByCart = CourseOrderRefunds.ByCart(snapshot);

        return settleable
            .Select(row => ToCandidate(row, buyers, payments, refundsByCart))
            .OfType<SettlementOrderResponse>()
            .OrderBy(order => order.OrderCreatedAtUtc)
            .ThenBy(order => order.OrderId, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// سفارشی که سهم پرداختش ثبت نشده یا جمع سهم‌هایش با مبلغ خالص سفارش نمی‌خواند، قابل تسویه نیست.
    /// </summary>
    private static SettlementOrderResponse? ToCandidate(
        CourseOrderReadRow row,
        IReadOnlyDictionary<Guid, CourseOrderBuyer> buyers,
        OrderSettlementPayments payments,
        IReadOnlyDictionary<Guid, decimal> refundsByCart)
    {
        var order = row.Details;
        var shares = OrderSettlementShares.Calculate(order, payments);
        if (shares is not { } netShares)
        {
            return null;
        }

        var netAmount = CourseOrderSettlementRule.NetAmount(order, CourseOrderRefunds.For(order, refundsByCart));
        if (netShares.RegularInNoor + netShares.GiftInNoor != netAmount)
        {
            return null;
        }

        return new SettlementOrderResponse(
            order.OrderId,
            order.SourceType,
            buyers[row.BuyerId].ExternalUserId,
            order.CreatedAtUtc,
            netShares.RegularInNoor,
            netShares.GiftInNoor,
            netAmount);
    }
}
