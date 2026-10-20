

namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourses;

public record PurchasedCourseItemResponse(
    Guid PurchaseId,
    string ExternalCourseId,
    string CourseTitle,
    decimal PriceInNoor,
    string Status);
