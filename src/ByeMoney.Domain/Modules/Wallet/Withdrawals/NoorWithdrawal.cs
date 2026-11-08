using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Domain.Modules.Wallet.Withdrawals;

/// <summary>
/// خروج نور عادی کاربر توسط ادمین و پرداخت ریالی دستی آن (D4، D09). نور هنگام ثبت کسر می‌شود؛
/// ثبت واریز اثر Ledger ندارد و «پرداخت نشد» نور را با ثبت جبرانی مرتبط برمی‌گرداند.
/// خود رکورد سابقهٔ کامل عملیات (علت، ذی‌نفع، اجراکنندهٔ هر مرحله) است.
/// </summary>
public class NoorWithdrawal : BaseEntity<NoorWithdrawalId>
{
    public const int DescriptionMaxLength = 1000;
    public const int BeneficiaryNameMaxLength = 200;
    public const int CardNumberLength = 16;
    public const int IbanLength = 26;
    public const int PaymentReferenceMaxLength = 100;
    public const int ReceiptIdMaxLength = 200;
    public const int ReversalReasonMaxLength = 500;

    public Guid OperationId { get; private set; }
    public UserId UserId { get; private set; }
    public NoorWithdrawalKind Kind { get; private set; }
    public TopUpRequestId? RelatedTopUpId { get; private set; }
    public decimal AmountNoor { get; private set; }

    /// <summary>مبلغ واقعی ریالی قابل واریز به ذی‌نفع؛ ادمین دستی وارد می‌کند؛ نور کسرشده از همین مبلغ حساب می‌شود (D09).</summary>
    public decimal AmountRial { get; private set; }

    public string BeneficiaryName { get; private set; } = null!;
    public string? BeneficiaryCardNumber { get; private set; }
    public string? BeneficiaryIban { get; private set; }
    public string Description { get; private set; } = null!;
    public UserId CreatedByUserId { get; private set; }

    /// <summary>شناسهٔ تراکنش Ledger کسر نور.</summary>
    public Guid LedgerTransactionId { get; private set; }

    public NoorWithdrawalStatus Status { get; private set; }

    public string? PaymentReference { get; private set; }
    public string? ReceiptId { get; private set; }
    public UserId? PaidByUserId { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }

    public string? ReversalReason { get; private set; }
    public Guid? ReversalTransactionId { get; private set; }
    public UserId? ReversedByUserId { get; private set; }
    public DateTime? ReversedAtUtc { get; private set; }

    private NoorWithdrawal() { }

    public static NoorWithdrawal Create(
        Guid operationId,
        UserId userId,
        NoorWithdrawalKind kind,
        TopUpRequestId? relatedTopUpId,
        decimal amountNoor,
        decimal amountRial,
        string beneficiaryName,
        string? beneficiaryCardNumber,
        string? beneficiaryIban,
        string description,
        UserId createdByUserId,
        Guid ledgerTransactionId)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Operation id is required.", nameof(operationId));
        }

        if (amountNoor <= 0 || amountRial <= 0)
        {
            throw new ArgumentException("Withdrawal amounts must be positive.", nameof(amountNoor));
        }

        if ((kind == NoorWithdrawalKind.DuplicatePaymentRefund) != relatedTopUpId.HasValue)
        {
            throw new ArgumentException("Only a duplicate-payment refund references a top-up.", nameof(relatedTopUpId));
        }

        if (string.IsNullOrWhiteSpace(beneficiaryCardNumber) && string.IsNullOrWhiteSpace(beneficiaryIban))
        {
            throw new ArgumentException("A card number or IBAN is required.", nameof(beneficiaryCardNumber));
        }

        return new NoorWithdrawal
        {
            Id = NoorWithdrawalId.New(),
            OperationId = operationId,
            UserId = userId,
            Kind = kind,
            RelatedTopUpId = relatedTopUpId,
            AmountNoor = amountNoor,
            AmountRial = amountRial,
            BeneficiaryName = beneficiaryName.Trim(),
            BeneficiaryCardNumber = Normalize(beneficiaryCardNumber),
            BeneficiaryIban = Normalize(beneficiaryIban),
            Description = description.Trim(),
            CreatedByUserId = createdByUserId,
            LedgerTransactionId = ledgerTransactionId,
            Status = NoorWithdrawalStatus.PendingPayment
        };
    }

    public void MarkPaid(string paymentReference, string receiptId, UserId paidByUserId)
    {
        EnsurePending();

        var now = DateTime.UtcNow;
        PaymentReference = paymentReference.Trim();
        ReceiptId = receiptId;
        PaidByUserId = paidByUserId;
        PaidAtUtc = now;
        Status = NoorWithdrawalStatus.Paid;
        UpdatedAt = now;
    }

    public void MarkReversed(string reason, Guid reversalTransactionId, UserId reversedByUserId)
    {
        EnsurePending();

        var now = DateTime.UtcNow;
        ReversalReason = reason.Trim();
        ReversalTransactionId = reversalTransactionId;
        ReversedByUserId = reversedByUserId;
        ReversedAtUtc = now;
        Status = NoorWithdrawalStatus.Reversed;
        UpdatedAt = now;
    }

    private void EnsurePending()
    {
        if (Status != NoorWithdrawalStatus.PendingPayment)
        {
            throw new InvalidOperationException("Only a withdrawal awaiting payment can change state.");
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
