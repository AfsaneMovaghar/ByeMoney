namespace ByeMoney.API.Contracts.CourseCarts;

public sealed record CourseCartErrorResponse(
    string Code,
    string Error);
