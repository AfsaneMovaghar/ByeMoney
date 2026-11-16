namespace ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;

/// <summary>
/// طرف ارسال/دریافت مرسوله به زبان دامنه؛ نگاشت به کد شهر یا شناسهٔ محلی هر واسط
/// داخل همان پیاده‌سازی انجام می‌شود تا افزودن واسط تازه فقط یک کلاینت بخواهد.
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
    string? PostalCode);

/// <summary>مشخصات بسته؛ واحد وزن گرم و ارزش اظهارشده به واحد پول همان واسط است.</summary>
public sealed record ShipmentParcelDetails(
    string IdempotencyKey,
    string? ItemTitle,
    int Quantity,
    int WeightGrams,
    long DeclaredValueMinorUnits,
    string CurrencyCode);

/// <summary>درخواست ساخت مرسوله؛ فرستنده از تنظیمات همان واسط خوانده می‌شود، نه از بدنهٔ عمومی.</summary>
public sealed record ShipmentCreateRequest(
    ShipmentParty Recipient,
    ShipmentParcelDetails Parcel,
    string? Remark = null);

public enum ShipmentCreateOutcome
{
    /// <summary>مرسوله تازه ساخته شد.</summary>
    Created,

    /// <summary>مرسوله از قبل با همان کلید یکتا وجود داشت و دوباره ساخته نشد.</summary>
    AlreadyExists,

    /// <summary>مرسوله ثبت شد ولی پرداخت نزد واسط معلق است (مثلاً موجودی ناکافی).</summary>
    PendingPayment,

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
    string? ErrorMessage = null);

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
    /// اول با کلید یکتا می‌خواند؛ اگر نبود می‌سازد. تکرار امن است و نباید مرسولهٔ دوم بسازد.
    /// </summary>
    Task<ShipmentCreateResult> CreateOrGetParcelAsync(ShipmentCreateRequest request, CancellationToken ct);
}
