using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Orders.Services;

internal static class CourseOrderPaging
{

    internal static PurchasePage<CourseOrderReadRow> Page(
        IReadOnlyList<CourseOrderReadRow> rows, CourseOrderFilter filter)
    {
        var filtered = rows.Where(row =>
            (!filter.FromUtc.HasValue || row.Details.CreatedAtUtc >= filter.FromUtc.Value.UtcDateTime) &&
            (!filter.ToUtc.HasValue || row.Details.CreatedAtUtc < filter.ToUtc.Value.UtcDateTime) &&
            (filter.FinancialStatus is null || row.Details.FinancialStatus == filter.FinancialStatus) &&
            (filter.DeliveryStatus is null || row.Details.DeliveryStatus == filter.DeliveryStatus) &&
            (filter.SourceType is null || row.Details.SourceType == filter.SourceType)).ToArray();
        var items = filtered.OrderByDescending(row => row.Details.CreatedAtUtc)
            .ThenByDescending(row => row.Details.OrderId, StringComparer.Ordinal)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToArray();
        return new PurchasePage<CourseOrderReadRow>(items, filter.Page, filter.PageSize, filtered.Length);
    }
}
