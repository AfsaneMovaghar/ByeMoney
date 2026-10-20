using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Queries.History;

public enum PurchaseFinancialStatus
{
    Paid
}

public enum PurchaseDeliveryStatus
{
    Pending,
    Delivered
}

public sealed record PurchasePage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record PurchaseListItem(
    Guid PurchaseId, Guid? CartId, string CourseTitle, decimal FinalAmountInNoor,
    DateTime PurchasedAtUtc, string FinancialStatus, string DeliveryStatus);

public sealed record AdminPurchaseListItem(
    Guid PurchaseId, Guid? CartId, string CourseTitle, decimal FinalAmountInNoor,
    DateTime PurchasedAtUtc, string FinancialStatus, string DeliveryStatus,
    string BuyerExternalUserId, string? BuyerDisplayName);

public sealed record PurchaseCartSummary(
    decimal OriginalTotalInNoor, decimal ItemDiscountInNoor,
    decimal CouponDiscountInNoor, decimal FinalAmountInNoor);

public sealed record PurchaseReceipt(
    Guid PurchaseId, Guid? CartId, Guid? TransactionId, string CourseExternalId,
    string CourseTitle, decimal? BaseAmountInNoor, decimal? DiscountAmountInNoor,
    string? CouponCode, decimal FinalAmountInNoor,
    decimal? ReferenceRialPerNoorAtPurchaseTime, DateTime PurchasedAtUtc,
    string FinancialStatus, string DeliveryStatus, DateTime? DeliveredAtUtc,
    PurchaseCartSummary? CartSummary);

public sealed record PurchaseHistoryFilter(
    int Page = 1, int PageSize = 20, string? BuyerExternalUserId = null,
    DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null,
    string? FinancialStatus = null, string? DeliveryStatus = null);

public sealed record PurchaseReadRow(
    Guid PurchaseId, Guid? CartId, string CourseTitle, decimal FinalAmountInNoor,
    DateTime PurchasedAtUtc, CoursePurchaseStatus Status,
    string BuyerExternalUserId, string? BuyerFirstName, string? BuyerLastName);

public sealed record PurchaseReceiptData(CoursePurchase Purchase, CourseCart? Cart);

public interface IPurchaseHistoryReader
{
    Task<PurchasePage<PurchaseReadRow>> ListAsync(
        Guid? buyerId, PurchaseHistoryFilter filter, CancellationToken ct);

    Task<PurchaseReceiptData?> GetReceiptAsync(Guid purchaseId, Guid buyerId, CancellationToken ct);
}

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
