namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;

public sealed class PhysicalRestockSettings
{
    public const string SectionName = "PhysicalRestock";

    /// <summary>
    /// سقف تلاش‌های ناموفق برگشت موجودی برای هر عملیات لغو. پس از رسیدن به سقف، سرویس پس‌زمینه عملیات را رها می‌کند
    /// و فقط در فهرست ادمین می‌ماند؛ ۲۰ تلاش با دورهٔ یک‌دقیقه‌ای حدود ۲۰ دقیقه قطعی استرپی را پوشش می‌دهد.
    /// </summary>
    public int MaxAttempts { get; set; } = 20;
}
