namespace ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;

/// <summary>نمای ادمین یک خروج نور؛ کاربر و اجراکننده‌ها با شناسهٔ استرپی برگردانده می‌شوند.</summary>
public sealed record NoorWithdrawalResponse(
    Guid Id,
    Guid OperationId,
    string UserExternalId,
    string Kind,
    string Status,
    Guid? RelatedTopUpId,
    decimal AmountNoor,
    decimal AmountRial,
    string BeneficiaryName,
    string? BeneficiaryCardNumber,
    string? BeneficiaryIban,
    string Description,
    string CreatedByExternalUserId,
    DateTime CreatedAtUtc,
    string? PaymentReference,
    bool HasReceipt,
    string? PaidByExternalUserId,
    DateTime? PaidAtUtc,
    string? ReversalReason,
    string? ReversedByExternalUserId,
    DateTime? ReversedAtUtc);
