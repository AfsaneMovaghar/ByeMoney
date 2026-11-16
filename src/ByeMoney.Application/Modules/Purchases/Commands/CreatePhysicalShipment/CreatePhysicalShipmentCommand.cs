using ByeMoney.Application.Modules.Purchases.Commands.UpdatePhysicalDeliveryStatus;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CreatePhysicalShipment;

/// <summary>
/// ساخت مرسوله نزد واسط پستی برای یک قلم کالای فیزیکی و ثبت وضعیت «ارسال‌شده».
/// <c>Provider</c> اختیاری است؛ پیش‌فرض اولین واسط پیکربندی‌شدهٔ ساخت مرسوله است.
/// </summary>
public sealed record CreatePhysicalShipmentCommand(Guid PurchaseId, string? Provider = null) :
    IRequest<CreatePhysicalShipmentResponse>;

public sealed record CreatePhysicalShipmentResponse(
    Guid PurchaseId,
    string DeliveryStatus,
    DateTime? ShippedAtUtc,
    string? TrackingCode,
    string? TrackingProvider,
    string? ProviderParcelId,
    string Outcome,
    string? Warning = null)
{
    public static CreatePhysicalShipmentResponse From(
        PhysicalDeliveryStatusResponse delivery,
        string? providerParcelId,
        string outcome,
        string? warning = null) =>
        new(delivery.PurchaseId, delivery.DeliveryStatus, delivery.ShippedAtUtc,
            delivery.TrackingCode, delivery.TrackingProvider, providerParcelId, outcome, warning);
}
