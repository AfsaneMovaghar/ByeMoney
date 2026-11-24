using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Wallet.Withdrawals;

namespace ByeMoney.Application.Modules.Wallet.Withdrawals.Interfaces;

/// <summary>
/// پایدارسازی خروج نور. قواعد مالی در Application می‌مانند؛ این Store فقط تراکنش، قفل و کوئری‌ها را فراهم می‌کند.
/// </summary>
public interface INoorWithdrawalStore
{
    /// <summary>
    /// اجرای کار در یک تراکنش، زیر قفل خریدار مشترک با مسیرهای خرید، تا خروج و خرید هم‌زمان روی یک کیف پول سریال شوند.
    /// نتیجهٔ ناموفق یا تداخل هم‌زمانی هیچ تغییری ذخیره نمی‌کند.
    /// </summary>
    Task<Result<T>> ExecuteAsync<T>(UserId userId, Func<Task<Result<T>>> work, CancellationToken ct);

    Task<NoorWithdrawal?> FindByOperationIdAsync(Guid operationId, CancellationToken ct);

    /// <summary>رکورد خروج با ردیابی تغییر، برای تغییر وضعیت.</summary>
    Task<NoorWithdrawal?> FindAsync(NoorWithdrawalId id, CancellationToken ct);

    Task<UserId?> FindOwnerAsync(NoorWithdrawalId id, CancellationToken ct);

    /// <summary>آیا برای این شارژ استرداد برنگشته‌ای (در انتظار یا پرداخت‌شده) وجود دارد.</summary>
    Task<bool> HasActiveRefundForTopUpAsync(TopUpRequestId topUpId, CancellationToken ct);

    Task<bool> IsReceiptReferencedAsync(string receiptId, CancellationToken ct);

    Task<string?> GetReceiptIdAsync(NoorWithdrawalId id, string scopeId, CancellationToken ct);

    void Add(NoorWithdrawal withdrawal);

    Task<NoorWithdrawalResponse?> GetResponseAsync(NoorWithdrawalId id, string scopeId, CancellationToken ct);

    /// <summary>شارژهای تأییدشدهٔ دارای نرخ ثبت‌شدهٔ یک کاربر، جدیدترین اول؛ مبنای انتخاب در استرداد.</summary>
    Task<IReadOnlyList<RefundableTopUpResponse>> ListRefundableTopUpsAsync(
        string userExternalId, int limit, string scopeId, CancellationToken ct);

    Task<IReadOnlyList<NoorWithdrawalResponse>> ListAsync(
        NoorWithdrawalStatus? status, string? userExternalId, int limit, string scopeId, CancellationToken ct);
}
