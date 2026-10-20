namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record CourseOrderFilter(
    int Page = 1, int PageSize = 20, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null,
    string? FinancialStatus = null, string? DeliveryStatus = null, string? SourceType = null);
