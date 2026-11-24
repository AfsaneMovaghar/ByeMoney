namespace ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;

/// <summary>
/// طرف ارسال/دریافت مرسوله. نام شهر برای نمایش و بدنهٔ واسط است.
/// <c>CityDocumentId</c> شناسهٔ ردیف شهر خودمان است و فقط برای خواندن کد حامل به کار می‌رود؛ کد حامل اینجا نیست.
/// </summary>
public sealed record ShipmentParty(
    string FullName,
    string Mobile,
    string? Telephone,
    string? NationalCode,
    string? CompanyName,
    string Address,
    string CityName,
    string ProvinceName,
    string? PostalCode,
    string? CityDocumentId = null);

/// <summary>یک ردیف داخل مرسوله؛ وزن و ارزش از قبل با تعداد باقی‌مانده مقیاس شده‌اند.</summary>
public sealed record ShipmentParcelItem(
    string? Description,
    int Quantity,
    int WeightGrams,
    long ValueIrr);

/// <summary>
/// مشخصات یک مرسولهٔ سبد. ابعاد از جدول جعبهٔ استاندارد است، نه از ادعای کلاینت.
/// </summary>
public sealed record ShipmentParcelDetails(
    string IdempotencyKey,
    IReadOnlyList<ShipmentParcelItem> Items,
    int BoxTypeId,
    int LengthCm,
    int WidthCm,
    int HeightCm,
    int TotalWeightGrams,
    long TotalValueIrr,
    bool IsFragile);

/// <summary>
/// درخواست ساخت مرسوله؛ فرستنده از پروفایل مبدأ فریزشدهٔ سبد در تنظیمات همان واسط خوانده می‌شود.
/// </summary>
public sealed record ShipmentCreateRequest(
    ShipmentParty Recipient,
    ShipmentParcelDetails Parcel,
    string? Remark = null,
    string? OriginId = null,
    string? CollectionType = null,
    string? CourierCode = null,
    string? ServiceType = null);

public enum ShipmentCreateOutcome
{
    /// <summary>مرسوله تازه ساخته شد.</summary>
    Created,

    /// <summary>مرسوله از قبل با همان کلید یکتا وجود داشت و دوباره ساخته نشد.</summary>
    AlreadyExists,

    /// <summary>مرسوله ثبت شد ولی پرداخت نزد واسط معلق است (مثلاً موجودی ناکافی).</summary>
    PendingPayment,

    /// <summary>واسط با همین کلید یکتا مرسوله‌ای نمی‌شناسد؛ ساخت دوباره مجاز است.</summary>
    NotFound,

    /// <summary>
    /// درخواست ساخت فرستاده شده و پاسخ قطعی نیست (تایم‌اوت، خطای سرور، یا نتیجه‌ای که خواندنش ناموفق بود).
    /// رزرو ساخت باید بماند و تلاش بعدی اول بخواند.
    /// </summary>
    Uncertain,

    /// <summary>واسط پیکربندی نشده یا خاموش است.</summary>
    NotConfigured,

    /// <summary>ساخت ناموفق؛ جزئیات در پیام خطا.</summary>
    Failed
}

public sealed record ShipmentCreateResult(
    ShipmentCreateOutcome Outcome,
    string? ProviderParcelId = null,
    string? TrackingCode = null,
    string? StatusName = null,
    string? ErrorMessage = null,
    string? TrackingUrl = null);

/// <summary>
/// ساخت مرسوله نزد یک واسط پستی. هر پیاده‌سازی کلید منبع خودش را اعلام می‌کند؛
/// سرویس فرمان ادمین واسط را با همان کلید انتخاب می‌کند تا افزودن فروشندهٔ سوم
/// فقط ثبت یک کلاینت تازه در DI باشد (هم‌تراز <see cref="ITrackingProviderClient"/>).
/// </summary>
public interface IShipmentProviderClient
{
    string Provider { get; }

    bool IsConfigured { get; }

    /// <summary>
    /// اول با کلید یکتا می‌خواند؛ فقط پاسخ ۴۰۴ یعنی مرسوله نیست. خطای خواندن ساخت را شروع نمی‌کند.
    /// تکرار نباید مرسولهٔ دوم بسازد. <see cref="ShipmentCreateOutcome.PendingPayment"/> ارسال‌شده نیست.
    /// </summary>
    Task<ShipmentCreateResult> CreateOrGetParcelAsync(ShipmentCreateRequest request, CancellationToken ct);

    /// <summary>
    /// خواندن مرسوله با همان کلید یکتای ساخت، بدون ساخت یا تغییر چیزی؛
    /// برای «بررسی وضعیت» پس از شارژ کیف پول واسط.
    /// </summary>
    Task<ShipmentCreateResult> GetParcelAsync(string idempotencyKey, CancellationToken ct);
}
