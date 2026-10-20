namespace ByeMoney.API.Contracts.CourseCarts;

public sealed record PrepareCourseCartRequest(
    Guid CartId,
    string[] ExternalCourseIds,
    string? CouponCode);
