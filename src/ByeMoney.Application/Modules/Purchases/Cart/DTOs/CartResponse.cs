namespace ByeMoney.Application.Modules.Purchases.Cart.DTOs;

public sealed record CartResponse(
    Guid CartId,
    string Status,
    string? CouponCode,
    decimal OriginalTotalInNoor,
    decimal ItemDiscountInNoor,
    decimal CouponDiscountInNoor,
    decimal PriceInNoor,
    decimal BalanceInNoor,
    decimal ShortfallInNoor,
    decimal AmountRial,
    DateTime ExpiresAtUtc,
    Guid? TopUpRequestId,
    string? TopUpStatus,
    IReadOnlyList<CartCourseResponse> Courses,
    decimal ShippingAmountInNoor = 0m,
    string? ShippingRuleLabel = null);
