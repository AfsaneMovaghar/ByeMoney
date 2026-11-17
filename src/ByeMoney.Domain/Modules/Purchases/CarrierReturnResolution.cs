namespace ByeMoney.Domain.Modules.Purchases;

/// <summary>
/// تصمیم و تعیین‌تکلیف ادمین برای مرسولهٔ برگشتی از واسط پستی (D10).
/// </summary>
public enum CarrierReturnResolution
{
    /// <summary>
    /// تحویل کالا به خریدار به هر نحوی تأیید شد؛ وضعیت به Delivered تبدیل و تسویه فعال می‌شود.
    /// </summary>
    DeliveryConfirmed = 1,

    /// <summary>
    /// رسیدگی بدون تسویه بسته شد؛ کالا Shipped می‌ماند و این سفارش برای همیشه از تسویه خارج می‌شود.
    /// </summary>
    ClosedWithoutSettlement = 2
}

