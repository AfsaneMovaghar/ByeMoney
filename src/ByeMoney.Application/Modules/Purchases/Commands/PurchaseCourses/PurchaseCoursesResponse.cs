namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourses;

public record PurchaseCoursesResponse(
    Guid TransactionId,
    decimal TotalPriceInNoor,
    IReadOnlyList<PurchasedCourseItemResponse> Items,
    DateTime PurchasedAtUtc);

public record PurchasedCourseItemResponse(
    Guid PurchaseId,
    string ExternalCourseId,
    string CourseTitle,
    decimal PriceInNoor,
    string Status);

