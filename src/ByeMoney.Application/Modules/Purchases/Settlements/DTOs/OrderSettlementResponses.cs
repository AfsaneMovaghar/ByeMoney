using ByeMoney.Application.Modules.Purchases.History.DTOs;

namespace ByeMoney.Application.Modules.Purchases.Settlements.DTOs;

/// <summary>
/// یک سفارش قابل تسویه (پیش‌نمایش) یا تسویه‌شده (Snapshot لحظهٔ بستن دوره).
/// <c>NetAmountInNoor</c> جمع کل و همیشه برابر سهم عادی به‌علاوهٔ سهم هدیه است.
/// </summary>
public sealed record SettlementOrderResponse(
    string OrderId,
    string SourceType,
    string BuyerExternalUserId,
    DateTime OrderCreatedAtUtc,
    decimal RegularNetAmountInNoor,
    decimal GiftNetAmountInNoor,
    decimal NetAmountInNoor);

/// <summary><c>PreviewToken</c> اثر انگشت کل فهرست (نه فقط همین صفحه) است و در بستن دوره عیناً فرستاده می‌شود.</summary>
public sealed record OrderSettlementPreviewResponse(
    int OrderCount,
    decimal TotalRegularNetAmountInNoor,
    decimal TotalGiftNetAmountInNoor,
    decimal TotalNetAmountInNoor,
    string PreviewToken,
    PurchasePage<SettlementOrderResponse> Orders);

public sealed record OrderSettlementPeriodResponse(
    Guid PeriodId,
    string? Title,
    string? Note,
    int OrderCount,
    decimal TotalRegularNetAmountInNoor,
    decimal TotalGiftNetAmountInNoor,
    decimal TotalNetAmountInNoor,
    string ClosedByExternalUserId,
    DateTime ClosedAtUtc);
