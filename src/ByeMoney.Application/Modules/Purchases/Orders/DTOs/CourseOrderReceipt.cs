using ByeMoney.Application.Modules.Purchases.History.DTOs;

namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record CourseOrderReceipt(
    string OrderId, string SourceType, Guid? CartId, Guid? TransactionId, DateTime PurchasedAtUtc,
    string? CouponCode, decimal FinalAmountInNoor, string FinancialStatus, string DeliveryStatus,
    PurchaseCartSummary? CartSummary, IReadOnlyList<CourseOrderReceiptItem> Items);

public sealed record CourseOrderReceiptItem(
    Guid PurchaseId, string ExternalId, string Title, string ItemType, string? ParentExternalId,
    int Quantity, decimal? UnitPriceInNoor, decimal? BaseAmountInNoor, decimal? DiscountAmountInNoor,
    decimal FinalAmountInNoor, decimal? ReferenceRialPerNoorAtPurchaseTime,
    string DeliveryStatus, string? PhysicalDeliveryStatus, DateTime PurchasedAtUtc, DateTime? DeliveredAtUtc,
    int CancelledQuantity = 0);
