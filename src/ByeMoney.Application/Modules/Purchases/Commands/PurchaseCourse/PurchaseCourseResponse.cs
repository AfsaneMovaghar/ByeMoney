namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourse;

public record PurchaseCourseResponse(
    Guid PurchaseId,
    string ExternalCourseId,
    string CourseTitle,
    decimal PriceInNoor,
    string Status,
    DateTime PurchasedAtUtc);
