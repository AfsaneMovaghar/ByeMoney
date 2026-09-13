using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Modules.Purchases;

public class CoursePurchase : BaseEntity<CoursePurchaseId>
{
    /// <summary>Internal ByeMoney UserId of the buyer. Gifting is out of scope (Buyer == Recipient).</summary>
    public UserId BuyerId { get; private set; }

    /// <summary>Guards the purchase state machine: Pending -> Debited -> NotificationSent / NotificationFailed.</summary>
    public CoursePurchaseStatus Status { get; private set; }

    /// <summary>Immutable snapshot of course catalog and pricing at purchase moment.</summary>
    public ProductSnapshot Snapshot { get; private set; } = null!;

    /// <summary>Links this purchase to the balanced double-entry rows in LedgerEntries.</summary>
    public Guid? LedgerTransactionId { get; private set; }

    /// <summary>Number of times the outbound Strapi access notification was attempted (capped at 5).</summary>
    public int NotificationAttempts { get; private set; }

    /// <summary>UTC timestamp of the last attempt to notify TarhElahi.</summary>
    public DateTime? LastNotificationAttemptAtUtc { get; private set; }

    /// <summary>Last error message returned by Strapi or HTTP client when notifying course access.</summary>
    public string? NotificationFailureReason { get; private set; }

    /// <summary>UTC timestamp when the outbound notification successfully completed.</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    private CoursePurchase() { }

    public static CoursePurchase Create(UserId buyerId, ProductSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new CoursePurchase
        {
            Id = CoursePurchaseId.New(),
            BuyerId = buyerId,
            Status = CoursePurchaseStatus.Pending,
            Snapshot = snapshot,
            NotificationAttempts = 0
        };
    }

    public void MarkDebited(Guid ledgerTransactionId)
    {
        if (Status != CoursePurchaseStatus.Pending)
        {
            throw new DomainException(string.Format(
                DomainErrors.CoursePurchase_CannotDebitNonPending, Status));
        }

        if (ledgerTransactionId == Guid.Empty)
        {
            throw new DomainException(DomainErrors.CoursePurchase_InvalidLedgerTransactionId);
        }

        Status = CoursePurchaseStatus.Debited;
        LedgerTransactionId = ledgerTransactionId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkNotificationSent()
    {
        if (Status != CoursePurchaseStatus.Debited && Status != CoursePurchaseStatus.NotificationFailed)
        {
            throw new DomainException(string.Format(
                DomainErrors.CoursePurchase_CannotNotifyNonDebited, Status));
        }

        Status = CoursePurchaseStatus.NotificationSent;
        NotificationAttempts++;
        LastNotificationAttemptAtUtc = DateTime.UtcNow;
        NotificationFailureReason = null;
        CompletedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordNotificationFailure(string reason)
    {
        if (Status != CoursePurchaseStatus.Debited && Status != CoursePurchaseStatus.NotificationFailed)
        {
            throw new DomainException(string.Format(
                DomainErrors.CoursePurchase_CannotNotifyNonDebited, Status));
        }

        Status = CoursePurchaseStatus.NotificationFailed;
        NotificationAttempts++;
        LastNotificationAttemptAtUtc = DateTime.UtcNow;
        NotificationFailureReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }
}
