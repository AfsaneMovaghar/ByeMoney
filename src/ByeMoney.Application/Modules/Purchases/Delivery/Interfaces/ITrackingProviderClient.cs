namespace ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;

/// <summary>
/// یک مرسوله برای استعلام وضعیت از واسط پستی. <c>ProviderReference</c> شناسهٔ ارجاع همان واسط است
/// و <c>TrackingCode</c> بارکد یا کد رهگیری قلم.
/// </summary>
public sealed record TrackingLookup(string? ProviderReference, string? TrackingCode);

public enum TrackingOutcome
{
    /// <summary>مرسوله تحویل شده است.</summary>
    Delivered,

    /// <summary>مرسوله برگشتی/امتناع گیرنده است؛ وضعیت تحویل تغییر نمی‌کند و ادمین باید رسیدگی کند.</summary>
    Returned,

    /// <summary>هنوز در مسیر است.</summary>
    InTransit,

    /// <summary>واسط این مرسوله را نمی‌شناسد.</summary>
    NotFound,

    /// <summary>این واسط فعال یا پیکربندی‌شده نیست؛ استعلام انجام نشد.</summary>
    NotConfigured,

    /// <summary>خطای شبکه یا پاسخ نامعتبر؛ چرخهٔ بعدی دوباره تلاش می‌کند.</summary>
    Failed
}

public sealed record TrackingResult(
    TrackingOutcome Outcome,
    string? StatusName = null,
    string? StatusTitle = null,
    string? TrackingCode = null);

/// <summary>
/// استعلام وضعیت مرسوله از یک واسط پستی. هر پیاده‌سازی کلید منبع خودش را اعلام می‌کند تا
/// سرویس همگام‌سازی واسط را با منبع همان قلم انتخاب کند. خطای شبکه به نتیجهٔ <c>Failed</c>
/// تبدیل می‌شود تا فراخواننده هرگز به‌خاطر قطعی واسط، وضعیت تحویل را تغییر ندهد.
/// </summary>
public interface ITrackingProviderClient
{
    /// <summary>کلید منبع؛ همان مقدار ذخیره‌شده در <c>TrackingProvider</c> قلم کالا.</summary>
    string Provider { get; }

    /// <summary>واسط فعال و دارای کلید دسترسی است؛ در غیر این صورت هیچ درخواستی فرستاده نمی‌شود.</summary>
    bool IsConfigured { get; }

    Task<TrackingResult> GetTrackingAsync(TrackingLookup lookup, CancellationToken ct);
}
