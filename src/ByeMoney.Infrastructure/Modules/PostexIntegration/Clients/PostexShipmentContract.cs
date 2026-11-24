namespace ByeMoney.Infrastructure.Modules.PostexIntegration.Clients;

/// <summary>
/// مقادیر ثابت قرارداد پستکس. تنظیم نیستند: استعلام هزینه همین‌ها را می‌فرستد
/// و عوض‌کردنشان قیمت دیده‌شده را از مرسولهٔ ثبت‌شده جدا می‌کند.
/// کلیدهای JSON همان نام فیلد سند پستکس‌اند و عمداً در محل استفاده نوشته می‌شوند.
/// </summary>
internal static class PostexShipmentContract
{
    /// <summary>کانال ثبت از API؛ سند ثبت گروهی همین مقدار را می‌خواهد.</summary>
    public const string ApiChannel = "api";

    /// <summary>ارسال فقط داخل ایران است.</summary>
    public const string CountryCode = "IR";

    /// <summary>ارزش اظهارشده به ریال است.</summary>
    public const string ValueCurrency = "IRR";

    /// <summary>کرایه را فرستنده می‌پردازد؛ استعلام هم همین را می‌فرستد.</summary>
    public const string FreightPayer = "SENDER";

    /// <summary>پیش‌پرداخت نداریم.</summary>
    public const int PrepaidAmount = 0;

    /// <summary>مایع ارسال نمی‌شود؛ استعلام هم false می‌فرستد.</summary>
    public const bool ShipsLiquid = false;

    /// <summary>
    /// خدمات اضافه خاموش‌اند تا با استعلام یکی بمانند.
    /// نام شیء در ساخت <c>added_service</c> و در استعلام <c>value_added_service</c> است.
    /// </summary>
    public const bool RequestLabel = false;

    public const bool RequestPackaging = false;

    public const bool RequestSmsNotification = false;

    /// <summary>پستکس نام خالی را رد می‌کند؛ فقط وقتی پروفایل نام ندارد.</summary>
    public const string FallbackSenderFirstName = "فرستنده";

    public const string FallbackSenderLastName = "شرکت";

    public const string FallbackRecipientFirstName = "گیرنده";

    public const string FallbackRecipientLastName = "نامشخص";

    /// <summary>تکهٔ خطای پستکس وقتی دفتر تحویل لازم است و فهرست دفتر نداریم.</summary>
    public const string DropOffLocationMarker = "drop_off_location";

    public const string DropOffLocationMarkerPascal = "DropOffLocation";

    /// <summary>کد ۱ (والد یا خود وضعیت) و کد ۱۰۰۰۰ هر دو «در انتظار پرداخت» هستند.</summary>
    public const int PaymentPendingCode = 1;

    public const int PaymentPendingStatusCode = 10000;

    /// <summary>فقط وقتی کد عددی در پاسخ نیست.</summary>
    public const string PaymentPendingStatusName = "PAYMENT_PENDING";
}
