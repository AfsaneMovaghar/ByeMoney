using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.RefreshPhysicalShipment;

/// <summary>
/// «بررسی وضعیت» مرسولهٔ واسط برای قلمی که هنوز «آماده‌سازی» است؛
/// بدون ساخت و بدون اثر مالی، برای وقتی کیف پول واسط شارژ شده یا مرسوله از سوی واسط پذیرفته/حذف شده است.
/// </summary>
public sealed record RefreshPhysicalShipmentCommand(Guid PurchaseId) :
    IRequest<PhysicalShipmentStateResponse>;

/// <summary>نتیجهٔ بررسی؛ فقط برای نمایش و تصمیم ادمین است و پول جابه‌جا نمی‌کند.</summary>
public enum PhysicalShipmentRefreshOutcome
{
    /// <summary>مرسوله در واسط ساخته شده ولی هنوز پرداخت نشده است.</summary>
    PaymentPending,

    /// <summary>پذیرفته شد ولی بارکد واقعی هنوز نیامده است.</summary>
    AwaitingTrackingCode,

    /// <summary>با بارکد واقعی «ارسال‌شده» ثبت شد.</summary>
    Shipped,

    /// <summary>واسط مرسوله را نمی‌شناسد؛ ارجاع پاک شد.</summary>
    ParcelNotFound,

    /// <summary>وضعیت خوانده نشد؛ هیچ تغییری اعمال نشد.</summary>
    ReadFailed
}

public sealed record PhysicalShipmentStateResponse(
    Guid PurchaseId,
    string DeliveryStatus,
    string? TrackingCode,
    string? TrackingProvider,
    string? ProviderParcelId,
    DateTime? CarrierPaymentPendingSinceUtc,
    string Outcome,
    string? Warning = null);
