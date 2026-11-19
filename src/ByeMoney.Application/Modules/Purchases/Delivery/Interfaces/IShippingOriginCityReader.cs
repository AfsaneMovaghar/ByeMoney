namespace ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;

/// <summary>
/// شهر پیکربندی‌شدهٔ یک مبدأ در تنظیمات واسط پستی.
/// پیاده‌سازی در Infrastructure است؛ Application به کلاس پستکس وابسته نیست.
/// </summary>
public interface IShippingOriginCityReader
{
    /// <summary>شناسهٔ شهر مبدأ، یا null اگر پروفایل یا شهر معتبر نباشد.</summary>
    int? GetCityId(string? shippingOriginId);
}
