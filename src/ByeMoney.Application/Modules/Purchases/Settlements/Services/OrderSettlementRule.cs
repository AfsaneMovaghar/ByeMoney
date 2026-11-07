using ByeMoney.Application.Modules.Purchases.Orders.Constants;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Settlements.Services;

/// <summary>
/// قاعدهٔ «قابل تسویه» (D10): سفارش <c>Paid</c> که همهٔ کالاهای فیزیکیِ لغونشده‌اش ارسال یا تحویل شده‌اند.
/// قلم کاملاً لغوشده حساب نمی‌شود و سفارشی که هیچ قلم باقی‌مانده ندارد قابل تسویه نیست.
/// سفارش فقط‌دوره پس از پرداخت قابل تسویه است.
/// </summary>
public static class OrderSettlementRule
{
    public static bool IsSettleable(OrderDetails order)
    {
        if (order.FinancialStatus != nameof(OrderFinancialStatus.Paid) || !order.FinalAmountInNoor.HasValue)
        {
            return false;
        }

        var remainingItems = order.Items
            .Where(HasRemainingQuantity)
            .ToArray();

        return remainingItems.Length > 0 && remainingItems
            .Where(IsPhysical)
            .All(IsShippedOrDelivered);
    }

    /// <summary>نور خالص سفارش: مبلغ نهایی منهای برگشتی لغو کالا.</summary>
    public static decimal NetAmount(OrderDetails order, decimal refundedAmountInNoor)
    {
        var finalAmount = order.FinalAmountInNoor
            ?? throw new InvalidOperationException("A settleable order must have a final amount.");

        return Math.Max(0m, finalAmount - refundedAmountInNoor);
    }

    private static bool IsPhysical(OrderItem item) =>
        item.ItemType == nameof(ProductItemType.Product);

    private static bool HasRemainingQuantity(OrderItem item) =>
        !IsPhysical(item) || item.CancelledQuantity < item.Quantity;

    private static bool IsShippedOrDelivered(OrderItem item) =>
        item.PhysicalDeliveryStatus is nameof(PhysicalDeliveryStatus.Shipped) or nameof(PhysicalDeliveryStatus.Delivered);
}
