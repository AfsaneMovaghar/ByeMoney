using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Constants;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Orders.Services;

/// <summary>
/// وضعیت تجمیعی برگشت دستی نور مرسوله‌های بسته‌شده بدون تسویه برای نمایش در فهرست سفارش‌های ادمین.
/// اولویت: NotRefunded &gt; ManualReview &gt; Refunded &gt; None.
/// </summary>
public static class OrderClosedParcelRefundStatus
{
    public static string For(OrderDetails order)
    {
        var products = order.Items
            .Where(item => item.ItemType == nameof(ProductItemType.Product))
            .ToArray();

        if (products.Any(item => item.ClosedParcelRefundStatus == ClosedParcelRefundStatuses.NotRefunded))
        {
            return ClosedParcelRefundStatuses.NotRefunded;
        }

        if (products.Any(item => item.ClosedParcelRefundStatus == ClosedParcelRefundStatuses.ManualReview))
        {
            return ClosedParcelRefundStatuses.ManualReview;
        }

        if (products.Any(item => item.ClosedParcelRefundStatus == ClosedParcelRefundStatuses.Refunded))
        {
            return ClosedParcelRefundStatuses.Refunded;
        }

        return ClosedParcelRefundStatuses.None;
    }
}
