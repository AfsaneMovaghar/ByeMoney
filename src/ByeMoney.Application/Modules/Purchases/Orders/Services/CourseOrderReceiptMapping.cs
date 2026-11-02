using ByeMoney.Application.Modules.Purchases.Orders.Constants;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Orders.Services;

public static class CourseOrderReceiptMapping
{
    /// <summary>
    /// رسید فقط برای سفارش با موفقیت مالی کامل ساخته می‌شود؛ در غیر این صورت null برمی‌گردد.
    /// </summary>
    public static CourseOrderReceipt? Build(CourseOrderDetails order, CourseOrderSnapshot snapshot)
    {
        var isPaid = order.FinancialStatus == CourseOrderFinancialStatus.Paid.ToString();
        var isComplete = order.Items.All(item => item.PurchaseId is not null && item.FinalAmountInNoor is not null);
        if (!isPaid || !isComplete)
        {
            return null;
        }

        var coursePurchases = snapshot.Purchases.ToDictionary(purchase => purchase.Id.Value);
        var physicalPurchases = snapshot.PhysicalPurchases.ToDictionary(purchase => purchase.Id.Value);
        var items = order.Items.Select(item => MapItem(item, order.CreatedAtUtc, coursePurchases, physicalPurchases)).ToArray();
        var transactionIds = order.Items.Select(item => item.TransactionId).Distinct().ToArray();
        var cart = order.CartId.HasValue
            ? snapshot.Carts.SingleOrDefault(item => item.Id == order.CartId.Value)
            : null;
        var purchasedAt = items.Length == 0 ? order.CreatedAtUtc : items.Min(item => item.PurchasedAtUtc);

        return new CourseOrderReceipt(
            order.OrderId, order.SourceType, order.CartId,
            transactionIds.Length == 1 ? transactionIds[0] : null,
            purchasedAt, cart?.CouponCode,
            order.FinalAmountInNoor ?? items.Sum(item => item.FinalAmountInNoor),
            order.FinancialStatus, order.DeliveryStatus, order.CartSummary, items);
    }

    private static CourseOrderReceiptItem MapItem(
        CourseOrderItem item,
        DateTime orderCreatedAtUtc,
        IReadOnlyDictionary<Guid, CoursePurchase> coursePurchases,
        IReadOnlyDictionary<Guid, PhysicalProductPurchase> physicalPurchases)
    {
        var purchaseId = item.PurchaseId!.Value;
        var finalAmount = item.FinalAmountInNoor!.Value;
        var baseAmount = item.PriceInNoor * item.Quantity;
        var isPhysical = item.ItemType == nameof(ProductItemType.Product);
        CoursePurchase? course = null;
        PhysicalProductPurchase? physical = null;
        if (isPhysical)
        {
            physicalPurchases.TryGetValue(purchaseId, out physical);
        }
        else
        {
            coursePurchases.TryGetValue(purchaseId, out course);
        }

        var deliveredAt = course?.CompletedAtUtc ?? physical?.DeliveredAtUtc;

        return new CourseOrderReceiptItem(
            purchaseId, item.ExternalCourseId, item.Title ?? item.ExternalCourseId, item.ItemType,
            item.ParentExternalId, item.Quantity, item.PriceInNoor, baseAmount, baseAmount - finalAmount,
            finalAmount, course?.Snapshot.ReferenceRialPerNoorAtPurchaseTime, item.DeliveryStatus,
            item.PhysicalDeliveryStatus, item.PurchasedAtUtc ?? orderCreatedAtUtc, deliveredAt);
    }
}
