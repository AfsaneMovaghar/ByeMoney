using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Orders.Services;

/// <summary>
/// وضعیت تجمیعی هشدار خسارت یا مفقودی برای فهرست سفارش‌های ادمین.
/// مقادیر: None, PendingReview, DeliveryConfirmed, ClosedWithoutSettlement.
/// </summary>
public static class OrderCarrierAlertStatus
{
    public const string None = "None";
    public const string PendingReview = "PendingReview";
    public const string DeliveryConfirmed = "DeliveryConfirmed";
    public const string ClosedWithoutSettlement = "ClosedWithoutSettlement";

    public static string For(OrderDetails order)
    {
        var alerts = order.Items
            .Where(item => item.ItemType == nameof(ProductItemType.Product) &&
                item.CarrierAlertCode is PhysicalProductPurchase.DamagedParcelStatusCode
                    or PhysicalProductPurchase.MissingShipmentStatusCode)
            .ToArray();

        if (alerts.Length == 0)
        {
            return None;
        }

        if (alerts.Any(item => string.IsNullOrEmpty(item.CarrierAlertResolution)))
        {
            return PendingReview;
        }

        if (alerts.Any(item => string.Equals(
            item.CarrierAlertResolution,
            nameof(CarrierReturnResolution.ClosedWithoutSettlement),
            StringComparison.OrdinalIgnoreCase)))
        {
            return ClosedWithoutSettlement;
        }

        if (alerts.All(item => string.Equals(
            item.CarrierAlertResolution,
            nameof(CarrierReturnResolution.DeliveryConfirmed),
            StringComparison.OrdinalIgnoreCase)))
        {
            return DeliveryConfirmed;
        }

        return None;
    }
}
