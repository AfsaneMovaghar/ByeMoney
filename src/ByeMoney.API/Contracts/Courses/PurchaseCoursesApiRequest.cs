namespace ByeMoney.API.Contracts.Courses;

public record PurchaseCoursesApiRequest(
    IReadOnlyList<string> ExternalCourseIds);

