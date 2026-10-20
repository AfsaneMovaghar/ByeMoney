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
        row.PurchasedAtUtc, PurchaseFinancialStatus.Paid.ToString(), DeliveryStatus(row.Status));

    public static AdminPurchaseListItem ToAdminListItem(PurchaseReadRow row)
    {
        var name = $"{row.BuyerFirstName} {row.BuyerLastName}".Trim();
        return new AdminPurchaseListItem(
            row.PurchaseId, row.CartId, row.CourseTitle, row.FinalAmountInNoor,
            row.PurchasedAtUtc, PurchaseFinancialStatus.Paid.ToString(), DeliveryStatus(row.Status),
            row.BuyerExternalUserId, string.IsNullOrWhiteSpace(name) ? null : name);
    }

    public static PurchaseReceipt ToReceipt(PurchaseReceiptData data)
    {
        var purchase = data.Purchase;
        var snapshot = purchase.Snapshot;
        var cart = data.Cart;
        var baseAmount = cart?.Items.SingleOrDefault(
            item => item.ExternalCourseId == snapshot.ExternalProductId)?.PriceInNoor;
        var summary = cart is null ? null : new PurchaseCartSummary(
            cart.Items.Sum(item => item.PriceInNoor), cart.ItemDiscountInNoor,
            cart.CouponDiscountInNoor, cart.Total);

        return new PurchaseReceipt(
            purchase.Id.Value, purchase.CartId, purchase.LedgerTransactionId,
            snapshot.ExternalProductId, snapshot.ProductTitle, baseAmount,
            baseAmount - snapshot.PriceInNoorAtPurchaseTime, cart?.CouponCode,
            snapshot.PriceInNoorAtPurchaseTime, snapshot.ReferenceRialPerNoorAtPurchaseTime,
            snapshot.PurchasedAt, PurchaseFinancialStatus.Paid.ToString(),
            DeliveryStatus(purchase.Status), purchase.CompletedAtUtc, summary);
    }
}
