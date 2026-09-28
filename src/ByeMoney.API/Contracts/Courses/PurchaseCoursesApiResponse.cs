using ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourses;

namespace ByeMoney.API.Contracts.Courses;

public record PurchaseCoursesApiResponse(
    Guid TransactionId,
    decimal TotalPriceInNoor,
    IReadOnlyList<PurchasedCourseItemApiResponse> Items,
    DateTime PurchasedAtUtc)
{
    public static PurchaseCoursesApiResponse From(PurchaseCoursesResponse result) => new(
        result.TransactionId,
        result.TotalPriceInNoor,
        result.Items.Select(i => new PurchasedCourseItemApiResponse(
            i.PurchaseId,
            i.ExternalCourseId,
            i.CourseTitle,
            i.PriceInNoor,
            i.Status)).ToList(),
        result.PurchasedAtUtc);
}

public record PurchasedCourseItemApiResponse(
    Guid PurchaseId,
    string ExternalCourseId,
    string CourseTitle,
    decimal PriceInNoor,
    string Status);

