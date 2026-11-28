using System.Text.Json.Serialization;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Constants;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

/// <summary>
/// <c>Quantity</c> همان تعداد خرید است و با لغو تغییر نمی‌کند؛
/// <c>CancelledQuantity</c> تعداد لغوشدهٔ بخش کالای فیزیکی است (برای اقلام دوره همیشه 0).
/// قلم وقتی کاملاً لغو شده که <c>CancelledQuantity &gt;= Quantity</c>.
/// <c>RefundedRegularInNoor</c> و <c>RefundedGiftInNoor</c> جمع نور برگشتی (عادی و هدیه) این قلم‌اند؛ برای اقلام دوره همیشه 0.
/// </summary>
public sealed record OrderItem(
    string ExternalItemId, string? Title, decimal? PriceInNoor, decimal? FinalAmountInNoor,
    Guid? PurchaseId, Guid? TransactionId, DateTime? PurchasedAtUtc,
    string FinancialStatus, string DeliveryStatus,
    string ItemType = nameof(ProductItemType.Course), string? ParentExternalId = null,
    int Quantity = 1, string? PhysicalDeliveryStatus = null,
    int CancelledQuantity = 0,
    decimal RefundedRegularInNoor = 0m, decimal RefundedGiftInNoor = 0m,
    string? TrackingCode = null, string? TrackingProvider = null, DateTime? ShippedAtUtc = null,
    DateTime? CarrierReturnedAtUtc = null,
    string? CarrierReturnResolution = null,
    string? CarrierReturnResolutionNote = null,
    DateTime? CarrierReturnResolvedAtUtc = null,
    string? CarrierReturnResolvedByExternalUserId = null,
    string? TrackingUrl = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? CarrierAlertCode = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTime? CarrierAlertAtUtc = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CarrierAlertResolution = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CarrierAlertResolutionNote = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTime? CarrierAlertResolvedAtUtc = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CarrierAlertResolvedByExternalUserId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ProviderParcelId = null,
    string ClosedParcelRefundStatus = ClosedParcelRefundStatuses.None,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTime? ClosedParcelRefundedAtUtc = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    decimal? ClosedParcelRefundAmountInNoor = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ClosedParcelRefundReason = null);
