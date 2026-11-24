using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CreatePhysicalShipment;

/// <summary>
/// ساخت یک مرسوله برای همهٔ اقلام فیزیکیِ فعال یک سبد.
/// «ارسال‌شده» فقط وقتی ثبت می‌شود که کد پستی واقعی آمده باشد.
/// روش جمع‌آوری از تنظیم سراسری استرپی خوانده می‌شود و در این فرمان نیست.
/// <c>Provider</c> اختیاری است؛ پیش‌فرض اولین واسط پیکربندی‌شدهٔ ساخت مرسوله است.
/// <c>BoxTypeId</c> اختیاری است؛ نبودش یعنی انتخاب خودکار جعبهٔ مرسوله.
/// </summary>
public sealed record CreatePhysicalShipmentCommand(
    Guid CartId,
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
