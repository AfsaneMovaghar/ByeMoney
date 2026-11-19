using ByeMoney.Application.Modules.Purchases.History.DTOs;

namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record OrderReceipt(
    string OrderId, string SourceType, Guid? CartId, Guid? TransactionId, DateTime PurchasedAtUtc,
    string? CouponCode, decimal FinalAmountInNoor, string FinancialStatus, string DeliveryStatus,
    PurchaseCartSummary? CartSummary, IReadOnlyList<OrderReceiptItem> Items);

public sealed record OrderReceiptItem(
    Guid PurchaseId, string ExternalItemId, string Title, string ItemType, string? ParentExternalId,
    int Quantity, decimal? UnitPriceInNoor, decimal? BaseAmountInNoor, decimal? DiscountAmountInNoor,
    decimal FinalAmountInNoor, decimal? ReferenceRialPerNoorAtPurchaseTime,
    string DeliveryStatus, string? PhysicalDeliveryStatus, DateTime PurchasedAtUtc, DateTime? DeliveredAtUtc,
    int CancelledQuantity = 0,
    decimal RefundedRegularInNoor = 0m, decimal RefundedGiftInNoor = 0m,
    string? TrackingCode = null, string? TrackingProvider = null, DateTime? ShippedAtUtc = null,
    DateTime? CarrierReturnedAtUtc = null,
    string? CarrierReturnResolution = null,
    string? ProviderParcelId = null, DateTime? CarrierPaymentPendingSinceUtc = null);
