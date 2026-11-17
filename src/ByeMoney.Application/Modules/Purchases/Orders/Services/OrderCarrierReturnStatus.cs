using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Orders.Services;

/// <summary>
/// محاسبهٔ وضعیت تجمیعی برگشتی‌های سفارش برای نمایش در جدول سفارش‌های ادمین (D10).
/// مقادیر مجاز: None, PendingReview, DeliveryConfirmed, ClosedWithoutSettlement.
/// </summary>
public static class OrderCarrierReturnStatus
{
    public const string None = "None";
    public const string PendingReview = "PendingReview";
    public const string DeliveryConfirmed = "DeliveryConfirmed";
    public const string ClosedWithoutSettlement = "ClosedWithoutSettlement";

    public static string For(OrderDetails order)
    {
        var returned = order.Items
            .Where(item => item.ItemType == nameof(ProductItemType.Product) && item.CarrierReturnedAtUtc.HasValue)
            .ToArray();

        if (returned.Length == 0)
        {
            return None;
        }

        if (returned.Any(item => string.IsNullOrEmpty(item.CarrierReturnResolution)))
        {
            return PendingReview;
        }

        if (returned.Any(item => string.Equals(
            item.CarrierReturnResolution,
            nameof(CarrierReturnResolution.ClosedWithoutSettlement),
            StringComparison.OrdinalIgnoreCase)))
        {
            return ClosedWithoutSettlement;
        }

        if (returned.All(item => string.Equals(
            item.CarrierReturnResolution,
            nameof(CarrierReturnResolution.DeliveryConfirmed),
            StringComparison.OrdinalIgnoreCase)))
        {
            return DeliveryConfirmed;
        }

        return None;
    }
}

