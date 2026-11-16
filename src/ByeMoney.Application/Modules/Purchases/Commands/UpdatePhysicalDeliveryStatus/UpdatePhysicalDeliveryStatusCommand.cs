using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.UpdatePhysicalDeliveryStatus;

/// <summary>
/// <c>TrackingCode</c> و <c>TrackingProvider</c> اختیاری‌اند و فقط در انتقال به Shipped (یا اصلاح آن) به کار می‌روند.
/// <c>ProviderParcelId</c> شناسهٔ ارجاع مرسوله نزد واسط پستی همان منبع برای استعلام وضعیت است.
/// </summary>
public sealed record UpdatePhysicalDeliveryStatusCommand(
    Guid PurchaseId,
    string Status,
    string? TrackingCode = null,
    string? TrackingProvider = null,
    string? ProviderParcelId = null) :
    IRequest<PhysicalDeliveryStatusResponse>;

public sealed record PhysicalDeliveryStatusResponse(
    Guid PurchaseId, string DeliveryStatus, DateTime? ShippedAtUtc, DateTime? DeliveredAtUtc,
    string? TrackingCode = null, string? TrackingProvider = null);
