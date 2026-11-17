namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

/// <summary>
/// جزئیات سفارش برای ادمین. ادمین ثبت‌کننده و علت رایگانِ سفارش دستی فقط همین‌جا می‌آیند، نه در مسیر خریدار.
/// </summary>
public sealed record AdminOrderDetails(
    string BuyerExternalUserId,
    string? BuyerDisplayName,
    OrderDetails Order,
    OrderShippingAddress? ShippingAddress = null,
    string? CreatedByAdminExternalUserId = null,
    string? AdminFreeReason = null,
    ShippingOriginView? ShippingOrigin = null);
