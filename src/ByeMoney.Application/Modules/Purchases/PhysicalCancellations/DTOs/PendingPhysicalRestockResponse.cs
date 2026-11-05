namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;

/// <summary>یک سطر صف برگشت موجودی که هنوز تأیید نشده است (خروجی Store، پیش از گروه‌بندی عملیات).</summary>
public sealed record PendingRestockRow(
    Guid OperationId,
    Guid CartId,
    Guid PurchaseId,
    int Quantity,
    int Attempts,
    string? LastErrorCode,
    DateTime CreatedAtUtc,
    DateTime? LastAttemptAtUtc);

/// <summary>
/// عملیات لغو با برگشت موجودی ناتمام. <c>Abandoned</c> یعنی تلاش‌ها به سقف رسیده و سرویس پس‌زمینه دیگر تلاش نمی‌کند.
/// </summary>
public sealed record PendingPhysicalRestockResponse(
    Guid OperationId,
    Guid CartId,
    int Attempts,
    int MaxAttempts,
    bool Abandoned,
    string? LastErrorCode,
    DateTime CreatedAtUtc,
    DateTime? LastAttemptAtUtc,
    IReadOnlyList<PhysicalRestockItem> Items);
