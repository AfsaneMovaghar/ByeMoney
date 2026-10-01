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
    public UserId? CreatedByUserId { get; private set; }
    public ChargeType? ChargeType { get; private set; }
    public decimal? AmountRial { get; private set; }
    public decimal? RialPerNoorSnapshot { get; private set; }
    public string? ReceiptId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public string ClientReferenceCode { get; private set; } = null!;
    public TopUpStatus Status { get; private set; }
    public string? ExternalTransactionId { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }

    public IReadOnlyList<PendingItemSnapshot> PendingItems { get; private set; } = [];

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
        IReadOnlyList<PendingItemSnapshot>? pendingItems = null,
        decimal? rialPerNoor = null)
    {
        if (amount <= 0)
            throw new DomainException(DomainErrors.TopUpRequest_AmountMustBeGreaterThanZero);
        if (paymentMethod == PaymentMethod.Gateway && decimal.Truncate(amount) != amount)
            throw new DomainException(DomainErrors.TopUpRequest_AmountMustBeWholeNoor);
        if (paymentMethod == PaymentMethod.Gateway && rialPerNoor is null)
            throw new DomainException(DomainErrors.TopUpRequest_InvalidRate);
        if (rialPerNoor is not null && (rialPerNoor <= 0 || decimal.Truncate(rialPerNoor.Value) != rialPerNoor))
            throw new DomainException(DomainErrors.TopUpRequest_InvalidRate);

        var itemsList = pendingItems ?? Array.Empty<PendingItemSnapshot>();
        foreach (var item in itemsList)
        {
            if (string.IsNullOrWhiteSpace(item.ExternalId))
                throw new DomainException(DomainErrors.ProductSnapshot_ExternalProductIdRequired);

            if (item.PriceNoorSnapshot <= 0)
                throw new DomainException(DomainErrors.CoursePurchase_InvalidPrice);
            if (decimal.Truncate(item.PriceNoorSnapshot) != item.PriceNoorSnapshot)
                throw new DomainException(DomainErrors.TopUpRequest_AmountMustBeWholeNoor);
        }

        var refCode = string.IsNullOrWhiteSpace(clientReferenceCode)
            ? GenerateClientReferenceCode()
            : clientReferenceCode.Trim();

        return new TopUpRequest
        {
            Id = TopUpRequestId.New(),
            UserId = userId,
            Amount = amount,
            AmountRial = rialPerNoor is null ? null : checked(amount * rialPerNoor.Value),
            RialPerNoorSnapshot = rialPerNoor,
            PaymentMethod = paymentMethod,
            ClientReferenceCode = refCode,
            Status = TopUpStatus.Pending,
            ExternalTransactionId = externalTransactionId,
            PendingItems = itemsList.ToList()
        };
    }

    public static TopUpRequest CreateAdminCardToCard(
        UserId beneficiaryUserId,
        UserId createdByUserId,
        decimal amountToman,
        decimal rialPerNoor,
        string receiptId,
        string idempotencyKey,
        string? externalTransactionId = null)
    {
        if (amountToman <= 0)
            throw new DomainException(DomainErrors.TopUpRequest_AmountMustBeGreaterThanZero);
        if (rialPerNoor <= 0 || decimal.Truncate(rialPerNoor) != rialPerNoor)
            throw new DomainException(DomainErrors.TopUpRequest_InvalidRate);
        if (string.IsNullOrWhiteSpace(receiptId) || string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException(DomainErrors.TopUpRequest_ReceiptAndIdempotencyRequired);

        var amountRial = checked(amountToman * 10m);
        var noorAmount = decimal.Round(amountRial / rialPerNoor, 4, MidpointRounding.ToEven);
        if (noorAmount <= 0)
            throw new DomainException(DomainErrors.TopUpRequest_AmountMustBeGreaterThanZero);

        var now = DateTime.UtcNow;
        return new TopUpRequest
        {
            Id = TopUpRequestId.New(),
            UserId = beneficiaryUserId,
            CreatedByUserId = createdByUserId,
            ChargeType = global::ByeMoney.Domain.Modules.Wallet.TopUps.ChargeType.AdminAssistedCardToCard,
            Amount = noorAmount,
            AmountRial = amountRial,
            RialPerNoorSnapshot = rialPerNoor,
            ReceiptId = receiptId.Trim(),
            IdempotencyKey = idempotencyKey.Trim(),
            PaymentMethod = PaymentMethod.CardToCard,
            ClientReferenceCode = GenerateClientReferenceCode(),
            Status = TopUpStatus.Confirmed,
            ExternalTransactionId = externalTransactionId,
            ConfirmedAtUtc = now
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
