using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Cart.DTOs;

public sealed record CartQuote(Guid CartId, string BuyerExternalUserId, string? CouponCode, IReadOnlyList<CourseCartQuoteItem> Items);
