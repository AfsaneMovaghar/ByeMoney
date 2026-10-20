namespace ByeMoney.Application.Modules.Purchases.Cart.DTOs;

public sealed record CartQuoteRequest(Guid CartId, string BuyerExternalUserId, string[] ExternalCourseIds, string? CouponCode);
