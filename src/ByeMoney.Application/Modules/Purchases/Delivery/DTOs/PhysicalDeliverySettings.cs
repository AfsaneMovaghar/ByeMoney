namespace ByeMoney.Application.Modules.Purchases.Delivery.DTOs;

/// <summary>
/// تنظیمات مشترک تحویل کالای فیزیکی، مستقل از واسط پستی؛ سیاست کاری اینجاست و
/// تنظیمات اتصال هر واسط نزد پیاده‌سازی همان واسط در Infrastructure می‌ماند.
/// </summary>
public sealed class PhysicalDeliverySettings
{
    public const string SectionName = "PhysicalDelivery";

    /// <summary>
    /// مهلت پشتیبان تحویل خودکار برای قلمی که استعلام واسط برایش ممکن نیست؛
    /// صفر یعنی خاموش.
    /// </summary>
    public int AutoConfirmAfterDays { get; set; } = 14;

    /// <summary>فاصلهٔ چرخهٔ استعلام وضعیت مرسوله‌ها در میزبان پس‌زمینه.</summary>
    public int PollIntervalMinutes { get; set; } = 60;
}
