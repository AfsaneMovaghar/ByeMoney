namespace ByeMoney.Application.Modules.Wallet.Withdrawals.Constants;

/// <summary>کدهای خطای ساختاریافتهٔ خروج نور؛ HTTP آن‌ها در کنترلر از وضعیت نتیجه نگاشت می‌شود.</summary>
public static class NoorWithdrawalErrorCodes
{
    public const string NotFound = "NOOR_WITHDRAWAL_NOT_FOUND";
    public const string UserNotFound = "NOOR_WITHDRAWAL_USER_NOT_FOUND";
    public const string InsufficientBalance = "NOOR_WITHDRAWAL_INSUFFICIENT_BALANCE";
    public const string TopUpInvalid = "NOOR_WITHDRAWAL_TOPUP_INVALID";
    public const string AmountExceedsTopUp = "NOOR_WITHDRAWAL_AMOUNT_EXCEEDS_TOPUP";
    public const string TopUpAlreadyRefunded = "NOOR_WITHDRAWAL_TOPUP_ALREADY_REFUNDED";
    public const string RateUnavailable = "NOOR_WITHDRAWAL_RATE_UNAVAILABLE";
    public const string AmountTooSmall = "NOOR_WITHDRAWAL_AMOUNT_TOO_SMALL";
    public const string NotPending = "NOOR_WITHDRAWAL_NOT_PENDING";
    public const string IdempotencyConflict = "NOOR_WITHDRAWAL_IDEMPOTENCY_CONFLICT";
    public const string Conflict = "NOOR_WITHDRAWAL_CONFLICT";
    public const string InvalidReceipt = "NOOR_WITHDRAWAL_INVALID_RECEIPT";
}
