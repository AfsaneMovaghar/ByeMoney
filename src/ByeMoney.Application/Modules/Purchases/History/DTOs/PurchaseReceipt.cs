namespace ByeMoney.Application.Modules.Purchases.History.DTOs;

public sealed record PurchaseReceipt(
    Guid PurchaseId, Guid? CartId, Guid? TransactionId, string CourseExternalId,
    string CourseTitle, decimal? BaseAmountInNoor, decimal? DiscountAmountInNoor,
    string? CouponCode, decimal FinalAmountInNoor,
    decimal? ReferenceRialPerNoorAtPurchaseTime, DateTime PurchasedAtUtc,
    string FinancialStatus, string DeliveryStatus, DateTime? DeliveredAtUtc,
    PurchaseCartSummary? CartSummary, string ItemType = "Course", string? ParentExternalId = null);
