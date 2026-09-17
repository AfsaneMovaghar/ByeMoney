using System.Security.Cryptography;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Modules.Wallet.TopUps;

public class TopUpRequest : BaseEntity<TopUpRequestId>
{
    private static readonly char[] Base32Chars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ".ToCharArray();

    public UserId UserId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public string ClientReferenceCode { get; private set; } = null!;
    public TopUpStatus Status { get; private set; }
    public string? ExternalTransactionId { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }

    public PendingItemType? PendingItemType { get; private set; }
    public string? PendingItemExternalId { get; private set; }
    public decimal? PendingPriceSnapshot { get; private set; }
    public decimal? PendingRateSnapshot { get; private set; }

    private TopUpRequest() { }

    public static string GenerateClientReferenceCode()
    {
        return $"TR-{RandomNumberGenerator.GetString(Base32Chars, 8)}";
    }

    public static TopUpRequest Create(
        UserId userId,
        decimal amount,
        PaymentMethod paymentMethod,
        string? externalTransactionId = null,
        string? clientReferenceCode = null,
        PendingItemType? pendingItemType = null,
        string? pendingItemExternalId = null,
        decimal? pendingPriceSnapshot = null,
        decimal? pendingRateSnapshot = null)
    {
        if (amount <= 0)
            throw new DomainException(DomainErrors.TopUpRequest_AmountMustBeGreaterThanZero);

        if (pendingItemType.HasValue)
        {
            if (string.IsNullOrWhiteSpace(pendingItemExternalId))
                throw new DomainException(DomainErrors.ProductSnapshot_ExternalProductIdRequired);

            if (!pendingPriceSnapshot.HasValue || pendingPriceSnapshot.Value <= 0)
                throw new DomainException(DomainErrors.CoursePurchase_InvalidPrice);

            if (!pendingRateSnapshot.HasValue || pendingRateSnapshot.Value <= 0)
                throw new DomainException(DomainErrors.CoursePurchase_InvalidConversionRate);
        }

        var refCode = string.IsNullOrWhiteSpace(clientReferenceCode)
            ? GenerateClientReferenceCode()
            : clientReferenceCode.Trim();

        return new TopUpRequest
        {
            Id = TopUpRequestId.New(),
            UserId = userId,
            Amount = amount,
            PaymentMethod = paymentMethod,
            ClientReferenceCode = refCode,
            Status = TopUpStatus.Pending,
            ExternalTransactionId = externalTransactionId,
            PendingItemType = pendingItemType,
            PendingItemExternalId = pendingItemExternalId?.Trim(),
            PendingPriceSnapshot = pendingPriceSnapshot,
            PendingRateSnapshot = pendingRateSnapshot
        };
    }

    public Result Confirm(string externalTransactionId)
    {
        if (Status == TopUpStatus.Confirmed)
        {
            if (ExternalTransactionId == externalTransactionId)
            {
                return Result.Success();
            }

            return Result.Conflict(DomainErrors.TopUpRequest_AlreadyConfirmedDifferentExternalId);
        }

        if (Status == TopUpStatus.Rejected)
        {
            return Result.Failure(DomainErrors.TopUpRequest_CannotConfirmRejected);
        }

        if (Status == TopUpStatus.Pending)
        {
            Status = TopUpStatus.Confirmed;
            ExternalTransactionId = externalTransactionId;
            ConfirmedAtUtc = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;

            return Result.Success();
        }

        return Result.Failure(string.Format(DomainErrors.TopUpRequest_CannotConfirmStatus, Status));
    }

    public void Reject(string reason)
    {
        if (Status != TopUpStatus.Pending)
            throw new DomainException(string.Format(DomainErrors.TopUpRequest_CannotRejectStatus, Status));

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException(DomainErrors.TopUpRequest_RejectionReasonRequired);

        Status = TopUpStatus.Rejected;
        RejectionReason = reason;
        RejectedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
