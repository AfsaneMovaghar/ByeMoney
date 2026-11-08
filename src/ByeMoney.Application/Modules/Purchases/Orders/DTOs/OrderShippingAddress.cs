namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

/// <summary>
/// آدرس تحویل ثابت‌شدهٔ سفارش؛ فقط در جزئیات سفارش ادمین برمی‌گردد (D20 بند ۱۱).
/// </summary>
public sealed record OrderShippingAddress(
    string Version,
    string Province,
    string City,
    string? PostalCode,
    string FullAddress,
    string RecipientName,
    string RecipientPhone);
