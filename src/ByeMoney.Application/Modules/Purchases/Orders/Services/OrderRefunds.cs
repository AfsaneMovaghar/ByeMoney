using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Orders.Services;

/// <summary>
/// جمع برگشتی لغو کالای فیزیکی هر سفارش (سهم عادی و هدیهٔ اقلام به‌علاوهٔ برگشت ارسال)،
/// محدود به مبلغ نهایی همان سفارش (I11-T).
/// </summary>
public static class OrderRefunds
{
    public static IReadOnlyDictionary<Guid, decimal> ByCart(OrderSnapshot snapshot)
    {
        var totals = snapshot.PhysicalCancellations
            .GroupBy(cancellation => cancellation.CartId)
            .ToDictionary(group => group.Key, group => group.Sum(TotalRefund));

        if (snapshot.ClosedParcelRefunds is { Count: > 0 })
        {
            foreach (var group in snapshot.ClosedParcelRefunds.GroupBy(refund => refund.CartId))
            {
                var closedTotal = group.Sum(TotalClosedRefund);
                totals[group.Key] = totals.TryGetValue(group.Key, out var existing)
                    ? existing + closedTotal
                    : closedTotal;
            }
        }

        return totals;
    }

    public static decimal For(OrderDetails order, IReadOnlyDictionary<Guid, decimal> refundsByCart)
    {
        var refunded = order.CartId.HasValue && refundsByCart.TryGetValue(order.CartId.Value, out var total)
            ? total
            : 0m;

        return order.FinalAmountInNoor.HasValue
            ? Math.Min(refunded, order.FinalAmountInNoor.Value)
            : refunded;
    }

    private static decimal TotalRefund(PhysicalProductCancellation cancellation) =>
        cancellation.RegularRefundInNoor + cancellation.GiftRefundInNoor +
        cancellation.ShippingRegularRefundInNoor + cancellation.ShippingGiftRefundInNoor;

    private static decimal TotalClosedRefund(ClosedParcelRefund refund) =>
        refund.RegularRefundInNoor + refund.GiftRefundInNoor;
}
