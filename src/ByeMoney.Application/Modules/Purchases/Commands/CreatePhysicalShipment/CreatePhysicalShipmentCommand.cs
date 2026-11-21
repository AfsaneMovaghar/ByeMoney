using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CreatePhysicalShipment;

/// <summary>
/// ساخت یک مرسوله برای همهٔ اقلام فیزیکیِ فعال یک سبد.
/// «ارسال‌شده» فقط وقتی ثبت می‌شود که کد پستی واقعی آمده باشد.
/// <c>CollectionType</c> روش همین درخواست است (<c>pick_up</c> یا <c>courier_drop_off</c>).
/// <c>Provider</c> اختیاری است؛ پیش‌فرض اولین واسط پیکربندی‌شدهٔ ساخت مرسوله است.
/// <c>BoxTypeId</c> اختیاری است؛ نبودش یعنی انتخاب خودکار جعبهٔ مرسوله.
/// </summary>
public sealed record CreatePhysicalShipmentCommand(
    Guid CartId,
    string? CollectionType,
    string? Provider = null,
    int? BoxTypeId = null) :
    IRequest<CreatePhysicalShipmentResponse>;

public sealed record CreatePhysicalShipmentResponse(
    Guid CartId,
    string DeliveryStatus,
    DateTime? ShippedAtUtc,
    string? TrackingCode,
    string? TrackingProvider,
    string? ProviderParcelId,
    string Outcome,
    string? Warning = null,
    DateTime? CarrierPaymentPendingSinceUtc = null);
