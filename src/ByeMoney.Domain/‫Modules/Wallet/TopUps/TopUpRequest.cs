using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Domain.Modules.Wallet.TopUps;

public class TopUpRequest : BaseEntity<TopUpRequestId>
{
    public UserId UserId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public TopUpStatus Status { get; private set; }
    public string? ExternalTransactionId { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }

    private TopUpRequest() { }

    public static TopUpRequest Create(
        UserId userId,
        decimal amount,
        PaymentMethod paymentMethod,
        string? externalTransactionId = null)
    {
        if (amount <= 0)
            throw new DomainException("Top-up amount must be greater than zero.");

        return new TopUpRequest
        {
            Id = TopUpRequestId.New(),
            UserId = userId,
            Amount = amount,
            PaymentMethod = paymentMethod,
            Status = TopUpStatus.Pending,
            ExternalTransactionId = externalTransactionId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Confirm(string? externalTransactionId = null)
    {
        if (Status != TopUpStatus.Pending)
            throw new DomainException($"Cannot confirm top-up request in status '{Status}'. Only pending requests can be confirmed.");

        Status = TopUpStatus.Confirmed;
        ConfirmedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(externalTransactionId))
        {
            ExternalTransactionId = externalTransactionId;
        }
    }

    public void Reject(string reason)
    {
        if (Status != TopUpStatus.Pending)
            throw new DomainException($"Cannot reject top-up request in status '{Status}'. Only pending requests can be rejected.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Rejection reason is required.");

        Status = TopUpStatus.Rejected;
        RejectionReason = reason;
        RejectedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}

