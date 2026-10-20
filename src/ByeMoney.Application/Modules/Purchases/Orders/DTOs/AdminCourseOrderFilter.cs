namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record AdminCourseOrderFilter(
    int Page = 1, int PageSize = 20, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null,
    string? FinancialStatus = null, string? DeliveryStatus = null, string? SourceType = null,
    string? BuyerExternalUserId = null)
{
    public CourseOrderFilter ToFilter() => new(
        Page, PageSize, FromUtc, ToUtc, FinancialStatus, DeliveryStatus, SourceType);
}
