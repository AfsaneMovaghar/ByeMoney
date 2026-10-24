using ByeMoney.Application.Modules.Purchases.History.Constants;
using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.History.Services;

public static class PurchaseHistoryMapping
{
    public static string DeliveryStatus(CoursePurchaseStatus status) =>
        (status == CoursePurchaseStatus.NotificationSent
            ? PurchaseDeliveryStatus.Delivered
            : PurchaseDeliveryStatus.Pending).ToString();

    public static PurchaseListItem ToListItem(PurchaseReadRow row) => new(
        row.PurchaseId, row.CartId, row.CourseTitle, row.FinalAmountInNoor,
        row.PurchasedAtUtc, PurchaseFinancialStatus.Paid.ToString(), DeliveryStatus(row.Status),
        row.ItemType.ToString(), row.ParentExternalId);

    public static AdminPurchaseListItem ToAdminListItem(PurchaseReadRow row)
    {
        var name = $"{row.BuyerFirstName} {row.BuyerLastName}".Trim();
        return new AdminPurchaseListItem(
            row.PurchaseId, row.CartId, row.CourseTitle, row.FinalAmountInNoor,
            row.PurchasedAtUtc, PurchaseFinancialStatus.Paid.ToString(), DeliveryStatus(row.Status),
            row.BuyerExternalUserId, string.IsNullOrWhiteSpace(name) ? null : name,
            row.ItemType.ToString(), row.ParentExternalId);
    }

    public static PurchaseReceipt ToReceipt(PurchaseReceiptData data)
    {
        var cart = data.Cart;
        var summary = cart is null ? null : new PurchaseCartSummary(
            cart.Items.Sum(item => item.LineTotalInNoor),
            cart.ItemDiscountInNoor,
            cart.CouponDiscountInNoor,
            cart.Total);

        if (data.PhysicalPurchase is { } physical)
        {
            var lineTotal = physical.UnitPriceInNoor * physical.Quantity;
            var discount = lineTotal - physical.PaidAmountInNoor;

            return new PurchaseReceipt(
                physical.Id.Value,
                physical.CartId,
                physical.LedgerTransactionId,
                physical.ExternalProductId,
                physical.ProductTitle,
                lineTotal,
                discount,
                cart?.CouponCode,
                physical.PaidAmountInNoor,
                null,
                physical.CreatedAtUtc,
                PurchaseFinancialStatus.Paid.ToString(),
                PurchaseDeliveryStatus.Pending.ToString(),
                null,
                summary,
                ProductItemType.Product.ToString(),
                null);
        }

        var purchase = data.Purchase!;
        var snapshot = purchase.Snapshot;
        var baseAmount = cart?.Items.SingleOrDefault(
            item => item.ExternalCourseId == snapshot.ExternalProductId && item.ItemType == snapshot.ItemType)?.LineTotalInNoor
            ?? cart?.Items.SingleOrDefault(
            item => item.ExternalCourseId == snapshot.ExternalProductId)?.LineTotalInNoor;

        return new PurchaseReceipt(
            purchase.Id.Value,
            purchase.CartId,
            purchase.LedgerTransactionId,
            snapshot.ExternalProductId,
            snapshot.ProductTitle,
            baseAmount,
            baseAmount - snapshot.PriceInNoorAtPurchaseTime,
            cart?.CouponCode,
            snapshot.PriceInNoorAtPurchaseTime,
            snapshot.ReferenceRialPerNoorAtPurchaseTime,
            snapshot.PurchasedAt,
            PurchaseFinancialStatus.Paid.ToString(),
            DeliveryStatus(purchase.Status),
            purchase.CompletedAtUtc,
            summary,
            snapshot.ItemType.ToString(),
            snapshot.ExternalParentId);
    }
}

