using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.API.Contracts.Carts;

public sealed record PrepareCartRequest(
    Guid CartId,
    CartItemInput[]? Items = null,
    string? CouponCode = null);
