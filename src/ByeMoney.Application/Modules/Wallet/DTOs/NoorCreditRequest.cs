using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Ledgers;

namespace ByeMoney.Application.Modules.Wallet.DTOs;

/// <summary>
/// مدل درخواست افزایش اعتبار نور.
/// توجه: کنترل تکرارناپذیری (Idempotency) بر عهدهٔ فراخواننده (مانند ثبت یکتای دسته هدیه یا سند بالادستی) است.
/// <c>TransactionId</c> اختیاری است تا چند افزایش یک عملیات (مانند برگشت عادی و هدیهٔ یک لغو) زیر یک شناسهٔ تراکنش Ledger بمانند.
/// </summary>
public sealed record NoorCreditRequest(
    UserId UserId,
    decimal AmountNoor,
    LedgerBalanceType BalanceType,
    LedgerReferenceType ReferenceType,
    string? ReferenceId = null,
    UserId? PerformedByUserId = null,
    string? Reason = null,
    Guid? TransactionId = null
);

