using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Carts.DTOs;

/// <summary>
/// درخواست عام قیمت‌گذاری سبد. شکل سیمی درخواست فروشنده (مثلاً قالب قدیمی شناسه‌ها یا اقلام تایپ‌دار)
/// فقط در آداپتور فروشنده تعیین می‌شود.
/// </summary>
public sealed record CartQuoteRequest(
    Guid CartId,
    string BuyerExternalUserId,
    string? CouponCode,
    IReadOnlyList<CartItemInput> Items);
