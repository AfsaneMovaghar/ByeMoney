using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Constants;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Constants;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Orders.Services;

public static class OrderPaging
{

    /// <summary>
    /// <paramref name="awaitingShipment"/> و <paramref name="needsCarrierAction"/> فقط برای فهرست ادمین هستند:
    /// اولی سفارش‌های Paid با حداقل یک قلم کالای ارسال‌نشده و لغو‌نشده و دومی سفارش‌های دارای برگشتی/هشدار
    /// تعیین‌تکلیف‌نشده یا قلم بسته‌شده بدون تسویهٔ نیازمند برگشت دستی نور را نگه می‌دارد.
    /// </summary>
    public static PurchasePage<OrderReadRow> Page(
        IReadOnlyList<OrderReadRow> rows,
        OrderFilter filter,
        bool awaitingShipment = false,
        bool needsCarrierAction = false)
    {
        var filtered = rows.Where(row =>
            (!filter.FromUtc.HasValue || row.Details.CreatedAtUtc >= filter.FromUtc.Value.UtcDateTime) &&
            (!filter.ToUtc.HasValue || row.Details.CreatedAtUtc < filter.ToUtc.Value.UtcDateTime) &&
            (filter.FinancialStatus is null || row.Details.FinancialStatus == filter.FinancialStatus) &&
            (filter.DeliveryStatus is null || row.Details.DeliveryStatus == filter.DeliveryStatus) &&
            (filter.SourceType is null || row.Details.SourceType == filter.SourceType) &&
            (!awaitingShipment || AwaitsShipment(row.Details)) &&
            (!needsCarrierAction || NeedsCarrierAction(row.Details))).ToArray();
        var items = filtered.OrderByDescending(row => row.Details.CreatedAtUtc)
            .ThenByDescending(row => row.Details.OrderId, StringComparer.Ordinal)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToArray();
        return new PurchasePage<OrderReadRow>(items, filter.Page, filter.PageSize, filtered.Length);
    }

    private static bool AwaitsShipment(OrderDetails order) =>
        order.FinancialStatus == OrderFinancialStatus.Paid.ToString() &&
        order.Items.Any(item =>
            item.ItemType == nameof(ProductItemType.Product) &&
            item.CancelledQuantity < item.Quantity &&
            item.PhysicalDeliveryStatus is not (nameof(PhysicalDeliveryStatus.Shipped) or
                nameof(PhysicalDeliveryStatus.Delivered)));

    private static bool NeedsCarrierAction(OrderDetails order) =>
        OrderCarrierReturnStatus.For(order) == OrderCarrierReturnStatus.PendingReview ||
        OrderCarrierAlertStatus.For(order) == OrderCarrierAlertStatus.PendingReview ||
        OrderClosedParcelRefundStatus.For(order) == ClosedParcelRefundStatuses.NotRefunded;
}
