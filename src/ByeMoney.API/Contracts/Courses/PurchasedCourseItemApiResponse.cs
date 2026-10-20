namespace ByeMoney.API.Contracts.Courses;

public record PurchasedCourseItemApiResponse(
    Guid PurchaseId,
    string ExternalCourseId,
    string CourseTitle,
    decimal PriceInNoor,
    string Status);
