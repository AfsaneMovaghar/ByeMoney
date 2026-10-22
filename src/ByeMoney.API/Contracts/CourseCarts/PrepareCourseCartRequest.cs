using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.API.Contracts.CourseCarts;

public sealed record PrepareCourseCartRequest(
    Guid CartId,
    string[]? ExternalCourseIds = null,
    string? CouponCode = null,
    CartItemInput[]? Items = null);
