using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Cart.DTOs;

public sealed record CartQuote(
    Guid CartId,
    string BuyerExternalUserId,
    string? CouponCode,
    IReadOnlyList<CourseCartQuoteItem> Items,
    decimal OriginalTotalInNoor = 0m,
    decimal ItemDiscountInNoor = 0m,
    decimal CouponDiscountInNoor = 0m,
    decimal ShippingAmountInNoor = 0m,
    string? ShippingRuleLabel = null,
    decimal PriceInNoor = 0m);

