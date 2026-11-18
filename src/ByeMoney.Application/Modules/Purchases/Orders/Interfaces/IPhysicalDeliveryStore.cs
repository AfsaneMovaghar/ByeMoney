using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Orders.Interfaces;

/// <summary>
/// اجرای تغییر وضعیت تحویل زیر همان قفل‌های خریدار و سبدِ لغو کالای فیزیکی،
/// تا ارسال و لغو یک قلم هرگز هم‌زمان اثر نگذارند.
/// </summary>
public interface IPhysicalDeliveryStore
{
    Task<PhysicalPurchaseOwner?> FindOwnerAsync(Guid purchaseId, CancellationToken ct);

    /// <summary>مالک سبد؛ برای تأیید دریافت خریدار روی کل سفارش (سطح سبد) لازم است.</summary>
    Task<PhysicalPurchaseOwner?> FindCartOwnerAsync(Guid cartId, CancellationToken ct);

    /// <summary>همهٔ اقلام کالای فیزیکی یک سبد، همراه برای تغییر وضعیت (tracked).</summary>
    Task<IReadOnlyList<PhysicalProductPurchase>> GetPurchasesAsync(Guid cartId, CancellationToken ct);

    /// <summary>نسخهٔ ثابت آدرس تحویل سبد؛ برای ساخت مرسوله نزد واسط پستی.</summary>
    Task<ShippingAddress?> GetCartShippingAddressAsync(Guid cartId, CancellationToken ct);

    /// <summary>شناسهٔ مبدأ فریزشدهٔ سبد؛ باید با پروفایل فرستندهٔ واسط یکی باشد.</summary>
    Task<string?> GetCartShippingOriginIdAsync(Guid cartId, CancellationToken ct);

    /// <summary>
    /// قلم‌های Shipped با زمان ارسال تا <paramref name="shippedAtOrBeforeUtc"/>؛ قدیمی‌ترها اول برمی‌گردند.
    /// <paramref name="providers"/> کلیدهای منبع برای استعلام بیرونی است؛ فهرست خالی یعنی همهٔ منابع.
    /// </summary>
    Task<IReadOnlyList<PhysicalDeliveryCandidate>> GetShippedAsync(
        DateTime shippedAtOrBeforeUtc, IReadOnlyCollection<string> providers, int limit, CancellationToken ct);

    /// <summary>
    /// قلم‌های «آماده‌سازی» که شناسهٔ ارجاع واسط دارند و کد پستی واقعی هنوز نیامده است.
    /// </summary>
    Task<IReadOnlyList<PhysicalDeliveryCandidate>> GetPreparingWithCarrierReferenceAsync(
        IReadOnlyCollection<string> providers, int limit, CancellationToken ct);

    Task<T> ExecuteLockedAsync<T>(Guid buyerId, Guid cartId, Func<Task<T>> work, CancellationToken ct);
}

/// <summary>قلم نامزد تغییر خودکار وضعیت تحویل؛ بدون ردیابی تا خواندن داخل قفل انجام شود.</summary>
public sealed record PhysicalDeliveryCandidate(
    Guid PurchaseId, Guid BuyerId, Guid CartId,
    string? TrackingCode, string? TrackingProvider, string? ProviderParcelId, DateTime? ShippedAtUtc);
