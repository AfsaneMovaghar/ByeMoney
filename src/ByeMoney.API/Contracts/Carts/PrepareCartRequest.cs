using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.API.Contracts.Carts;

public sealed record PrepareCartRequest(
    Guid CartId,
    string[]? ExternalCourseIds = null,
    string? CouponCode = null,
    CartItemInput[]? Items = null);

