namespace ByeMoney.API.Contracts.Courses;

public record PurchaseCourseApiResponse(
    Guid PurchaseId,
    string ExternalCourseId,
    string CourseTitle,
    decimal PriceInNoor,
    string Status,
    DateTime PurchasedAtUtc);