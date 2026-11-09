namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

/// <summary>
/// آدرس تحویل ثابت‌شدهٔ سفارش برای خود خریدار؛ بدون <c>Version</c> (D20 بند ۱۱).
/// </summary>
public sealed record MyOrderShippingAddress(
    string RecipientName,
    string RecipientPhone,
    string Province,
    string City,
    string FullAddress,
    string? PostalCode);
