namespace ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;

/// <summary>
/// شارژ تأییدشدهٔ یک کاربر که می‌تواند مبنای «استرداد پرداخت تکراری» باشد؛
/// <c>HasActiveRefund</c> یعنی استرداد برنگشتهٔ دیگری برای آن ثبت شده است.
/// </summary>
public sealed record RefundableTopUpResponse(
    Guid Id,
    string ClientReferenceCode,
    decimal AmountNoor,
    decimal AmountRial,
    decimal RialPerNoor,
    DateTime? ConfirmedAtUtc,
    bool HasActiveRefund);
