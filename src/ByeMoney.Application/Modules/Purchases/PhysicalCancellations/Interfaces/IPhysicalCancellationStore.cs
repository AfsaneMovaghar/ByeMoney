using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;

public sealed record PhysicalPurchaseOwner(Guid BuyerId, Guid CartId);

/// <summary>
/// پایدارسازی لغو کالای فیزیکی. قواعد مالی در Application می‌مانند؛ این Store فقط تراکنش، قفل و کوئری‌ها را فراهم می‌کند.
/// </summary>
public interface IPhysicalCancellationStore
{
    Task<PhysicalPurchaseOwner?> FindOwnerAsync(Guid purchaseId, CancellationToken ct);

    Task<Cart?> FindCartAsync(Guid cartId, CancellationToken ct);

    /// <summary>اقلام فیزیکی سبد با ردیابی تغییر، به ترتیب پایدار شناسه.</summary>
    Task<IReadOnlyList<PhysicalProductPurchase>> GetPurchasesAsync(Guid cartId, CancellationToken ct);

    Task<IReadOnlyList<PhysicalProductCancellation>> GetOperationAsync(Guid operationId, CancellationToken ct);

    /// <summary>سهم عادی و هدیهٔ پرداخت ارسال، از Ledger سطح سبد خریدار.</summary>
    Task<PaymentShares> GetShippingPaymentAsync(Guid cartId, CancellationToken ct);

    void Add(PhysicalProductCancellation cancellation);

    /// <summary>درخواست برگشت موجودی عملیات، اگر هنوز تأیید نشده باشد؛ در غیر این صورت null.</summary>
    Task<PhysicalRestockRequest?> FindPendingRestockAsync(Guid operationId, CancellationToken ct);

    /// <summary>
    /// شناسهٔ عملیات‌های لغو که برگشت موجودی‌شان تأیید نشده و تلاش‌هایشان به سقف نرسیده است، قدیمی‌ترین اول.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetPendingRestockOperationsAsync(int limit, int maxAttempts, CancellationToken ct);

    Task MarkStockReturnedAsync(Guid operationId, CancellationToken ct);

    /// <summary>کد خطا را ثبت و شمارندهٔ تلاش‌های ناموفق عملیات را یکی زیاد می‌کند.</summary>
    Task RecordStockReturnFailureAsync(Guid operationId, string? errorCode, CancellationToken ct);

    /// <summary>سطرهای برگشت موجودی تأییدنشده (شامل رهاشده‌ها)، قدیمی‌ترین اول، برای فهرست ادمین.</summary>
    Task<IReadOnlyList<PendingRestockRow>> GetUnfinishedRestocksAsync(int limit, CancellationToken ct);

    /// <summary>
    /// اجرای کار در یک تراکنش محلی زیر قفل خریدار و سبد (همان قفل‌های پرداخت سبد). نتیجهٔ ناموفق یا تعارض
    /// پایدارسازی، کل تراکنش را برمی‌گرداند.
    /// </summary>
    Task<Result<T>> ExecuteAsync<T>(Guid buyerId, Guid cartId, Func<Task<Result<T>>> work, CancellationToken ct);
}
