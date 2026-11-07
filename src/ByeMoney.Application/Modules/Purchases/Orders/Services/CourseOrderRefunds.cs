using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Orders.Services;

/// <summary>
/// جمع برگشتی لغو کالای فیزیکی هر سفارش (سهم عادی و هدیهٔ اقلام به‌علاوهٔ برگشت ارسال)،
/// محدود به مبلغ نهایی همان سفارش (I11-T).
/// </summary>
public static class CourseOrderRefunds
{
    public static IReadOnlyDictionary<Guid, decimal> ByCart(CourseOrderSnapshot snapshot) =>
        snapshot.PhysicalCancellations
            .GroupBy(cancellation => cancellation.CartId)
            .ToDictionary(group => group.Key, group => group.Sum(TotalRefund));

    public static decimal For(CourseOrderDetails order, IReadOnlyDictionary<Guid, decimal> refundsByCart)
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
}
