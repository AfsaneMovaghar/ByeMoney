namespace ByeMoney.API.Contracts.Courses;

public record PurchaseCoursesApiResponse(
    Guid TransactionId,
    decimal TotalPriceInNoor,
    IReadOnlyList<PurchasedCourseItemApiResponse> Items,
    DateTime PurchasedAtUtc);

public record PurchasedCourseItemApiResponse(
    Guid PurchaseId,
    string ExternalCourseId,
    string CourseTitle,
    decimal PriceInNoor,
    string Status);

