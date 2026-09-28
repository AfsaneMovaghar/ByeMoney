namespace ByeMoney.API.Contracts.Courses;

public record AdminPurchaseCoursesApiRequest(
    string BeneficiaryExternalUserId,
    IReadOnlyList<string> ExternalCourseIds,
    bool IsFree = false,
    string? FreeReason = null);
