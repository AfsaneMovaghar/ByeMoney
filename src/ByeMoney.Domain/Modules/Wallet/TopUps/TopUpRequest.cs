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
    public string? GatewayName { get; private set; }
    public string? BankReferenceNumber { get; private set; }
    public string? GatewayResultCode { get; private set; }
    public string? GatewayResultKind { get; private set; }
    public string? GatewayEventId { get; private set; }
    public DateTime? GatewayResultAtUtc { get; private set; }
    public decimal? GatewayOriginalAmountRial { get; private set; }
    public decimal? GatewayAffectiveAmountRial { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }
    public string? ReviewCaseId { get; private set; }
    public string? ReviewReasonCode { get; private set; }
    public DateTime? ReviewOpenedAtUtc { get; private set; }
    public DateTime? ReviewResolvedAtUtc { get; private set; }
    public string? ReviewOutcomeCode { get; private set; }
    public string? ReviewResolutionFinancialReferenceId { get; private set; }
    public int ReviewRevision { get; private set; }
    public string? ManualRefundReference { get; private set; }
    public IReadOnlyList<FinancialReviewAudit> ReviewAudit { get; private set; } = [];

    public IReadOnlyList<PendingItemSnapshot> PendingItems { get; private set; } = [];

    private TopUpRequest() { }

    public Result OpenFinancialReview(string caseId, string reasonCode)
    {
        if (PaymentMethod != PaymentMethod.Gateway)
            return Result.Conflict(DomainErrors.TopUpRequest_PaymentMethodNotSupported);
        if (ReviewCaseId is not null)
            return ReviewCaseId == caseId && ReviewReasonCode == reasonCode
                ? Result.Success()
                : Result.Conflict(DomainErrors.TopUpRequest_ReviewConflict);

        ReviewCaseId = caseId;
        ReviewRevision = 1;
        ReviewReasonCode = reasonCode;
        ReviewOpenedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result ResolveFinancialReview(string caseId, string outcomeCode, string? financialReferenceId)
    {
        if (ReviewCaseId != caseId)
            return Result.Conflict(DomainErrors.TopUpRequest_ReviewConflict);
        if (ReviewResolvedAtUtc is not null)
            return ReviewOutcomeCode == outcomeCode &&
                   ReviewResolutionFinancialReferenceId == financialReferenceId
                ? Result.Success()
                : Result.Conflict(DomainErrors.TopUpRequest_ReviewConflict);

        var matching = outcomeCode switch
        {
            FinancialReviewOutcomeCodes.PaidAndConfirmed => Status == TopUpStatus.Confirmed &&
                GatewayResultKind == GatewayResultKinds.Verified &&
                ExternalTransactionId == financialReferenceId,
            FinancialReviewOutcomeCodes.UnpaidRejected => Status == TopUpStatus.Rejected &&
                GatewayResultKind == GatewayResultKinds.Unpaid && financialReferenceId is null,
            FinancialReviewOutcomeCodes.ReversedRejected => Status == TopUpStatus.Rejected &&
                GatewayResultKind == GatewayResultKinds.ReverseSucceeded &&
                ExternalTransactionId == financialReferenceId,
            _ => false
        };
        if (!matching)
            return Result.Conflict(DomainErrors.TopUpRequest_ReviewStateMismatch);

        ReviewOutcomeCode = outcomeCode;
        ReviewResolutionFinancialReferenceId = financialReferenceId;
        ReviewResolvedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result ResolveManualReview(string caseId, string outcomeCode, string? financialReferenceId,
        FinancialReviewEvidence evidence, Guid actorUserId, string? actorName)
    {
        var previous = ReviewAudit.FirstOrDefault(x => x.EventId == evidence.OperationId.ToString());
        if (previous is not null)
            return ReviewCaseId == caseId && previous.ActorUserId == actorUserId &&
                previous.OutcomeCode == outcomeCode && previous.FinancialReferenceId == financialReferenceId &&
                previous.Evidence == evidence ? Result.Success() : Result.Conflict(DomainErrors.TopUpRequest_ReviewConflict);
        if (ReviewCaseId != caseId || ReviewRevision != evidence.ExpectedRevision || ReviewResolvedAtUtc is not null)
            return Result.Conflict(DomainErrors.TopUpRequest_ReviewConflict);
        if (outcomeCode is FinancialReviewOutcomeCodes.NoMatchingDeposit or FinancialReviewOutcomeCodes.ManualRefund)
        {
            if (Status == TopUpStatus.Confirmed ||
                outcomeCode == FinancialReviewOutcomeCodes.NoMatchingDeposit && evidence.MatchingDepositFound != false ||
                outcomeCode == FinancialReviewOutcomeCodes.ManualRefund && string.IsNullOrWhiteSpace(evidence.ManualRefundReference))
                return Result.Conflict(DomainErrors.TopUpRequest_ReviewStateMismatch);
            ReviewOutcomeCode = outcomeCode;
            ReviewResolutionFinancialReferenceId = financialReferenceId;
            ReviewResolvedAtUtc = DateTime.UtcNow;
            if (outcomeCode == FinancialReviewOutcomeCodes.ManualRefund)
                ManualRefundReference = evidence.ManualRefundReference;
        }
        else
        {
            var result = ResolveFinancialReview(caseId, outcomeCode, financialReferenceId);
            if (result.IsFailure) return result;
        }
        ReviewAudit = [.. ReviewAudit, new FinancialReviewAudit(evidence.OperationId.ToString(), "resolved",
            ReviewRevision, ReviewResolvedAtUtc!.Value, actorUserId, actorName, outcomeCode,
            financialReferenceId, evidence, evidence.Note)];
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result ReopenFinancialReview(string caseId, string evidenceId, string? note)
    {
        if (ReviewCaseId != caseId) return Result.Conflict(DomainErrors.TopUpRequest_ReviewConflict);
        if (ReviewAudit.Any(x => x.EventId == evidenceId && x.EventType == "evidence")) return Result.Success();
        if (ReviewResolvedAtUtc is not null && !ReviewAudit.Any(x => x.EventType == "resolved" && x.Revision == ReviewRevision))
            ReviewAudit = [.. ReviewAudit, new FinancialReviewAudit($"legacy:{caseId}:{ReviewRevision}", "resolved",
                ReviewRevision, ReviewResolvedAtUtc.Value, null, null, ReviewOutcomeCode,
                ReviewResolutionFinancialReferenceId, null, null)];
        // نسخه با هر شاهد تازه تغییر می‌کند تا فرم قدیمی نتواند پرونده را ببندد.
        ReviewRevision++;
        ReviewAudit = [.. ReviewAudit, new FinancialReviewAudit(evidenceId, "evidence", ReviewRevision,
            DateTime.UtcNow, null, null, null, null, null, note)];
        ReviewResolvedAtUtc = null;
        ReviewOutcomeCode = null;
        ReviewResolutionFinancialReferenceId = null;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

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

    public static TopUpRequest CreateGateway(
        UserId userId,
        decimal amountNoor,
        decimal rialPerNoor,
        IReadOnlyList<PendingItemSnapshot>? pendingItems = null)
    {
        if (amountNoor <= 0 || decimal.Truncate(amountNoor) != amountNoor ||
            rialPerNoor <= 0 || decimal.Truncate(rialPerNoor) != rialPerNoor)
            throw new DomainException(DomainErrors.TopUpRequest_InvalidRate);

        return Create(userId, amountNoor, PaymentMethod.Gateway,
            pendingItems: pendingItems, rialPerNoor: rialPerNoor);
    }

    public Result ConfirmGateway(string externalTransactionId, string bankReferenceNumber, string gatewayName)
    {
        if (ManualRefundReference is not null)
            return Result.Conflict(DomainErrors.TopUpRequest_ReviewStateMismatch);
        if (Status == TopUpStatus.Confirmed)
            return ExternalTransactionId == externalTransactionId &&
                   BankReferenceNumber == bankReferenceNumber && GatewayName == gatewayName
                ? Result.Success()
                : Result.Conflict(DomainErrors.TopUpRequest_AlreadyConfirmedDifferentExternalId);
        if (Status == TopUpStatus.Rejected)
            return Result.Conflict(DomainErrors.TopUpRequest_CannotConfirmRejected);
        if (PaymentMethod != PaymentMethod.Gateway)
            return Result.Conflict(string.Format(DomainErrors.TopUpRequest_CannotConfirmStatus, Status));

        var result = Confirm(externalTransactionId);
        if (result.IsSuccess)
        {
            BankReferenceNumber = bankReferenceNumber;
            GatewayName = gatewayName;
        }
        return result;
    }

    public Result RecordGatewayResult(string eventId, string gateway, string kind, string? bankTransactionId,
        string? bankReferenceNumber, string? resultCode, decimal? originalAmountRial,
        decimal? affectiveAmountRial, DateTime occurredAtUtc)
    {
        if (PaymentMethod != PaymentMethod.Gateway)
            return Result.Conflict(DomainErrors.TopUpRequest_PaymentMethodNotSupported);
        if (GatewayEventId == eventId)
            return GatewayResultKind == kind && GatewayName == gateway &&
                   (kind == GatewayResultKinds.Unknown || ExternalTransactionId == bankTransactionId) &&
                   BankReferenceNumber == bankReferenceNumber &&
                   GatewayResultCode == resultCode && GatewayOriginalAmountRial == originalAmountRial &&
                   GatewayAffectiveAmountRial == affectiveAmountRial
                ? Result.Success()
                : Result.Conflict(DomainErrors.TopUpRequest_GatewayEventPayloadMismatch);
        if (kind == GatewayResultKinds.Unknown && Status != TopUpStatus.Pending)
            return Result.Success();
        if (Status == TopUpStatus.Confirmed && kind != GatewayResultKinds.Verified)
            return Result.Conflict(string.Format(DomainErrors.TopUpRequest_CannotRecordResultForStatus, kind, Status));
        if (Status == TopUpStatus.Rejected && kind == GatewayResultKinds.Verified)
            return Result.Conflict(DomainErrors.TopUpRequest_CannotConfirmRejected);
        if (Status == TopUpStatus.Confirmed && (ExternalTransactionId != bankTransactionId ||
            BankReferenceNumber != bankReferenceNumber || GatewayName != gateway ||
            GatewayResultKind is not null &&
            (GatewayResultCode != resultCode || GatewayOriginalAmountRial != originalAmountRial ||
             GatewayAffectiveAmountRial != affectiveAmountRial)))
            return Result.Conflict(DomainErrors.TopUpRequest_AlreadyConfirmedDifferentExternalId);
        if (Status == TopUpStatus.Rejected &&
            !(kind == GatewayResultKinds.ReverseSucceeded && GatewayResultKind == GatewayResultKinds.Unpaid) &&
            (GatewayResultKind != kind || GatewayResultCode != resultCode ||
             GatewayName != gateway || ExternalTransactionId != bankTransactionId ||
             GatewayOriginalAmountRial != originalAmountRial ||
             GatewayAffectiveAmountRial != affectiveAmountRial))
            return Result.Conflict(DomainErrors.TopUpRequest_GatewayEventPayloadMismatch);
        if (ExternalTransactionId is not null && bankTransactionId is not null &&
            ExternalTransactionId != bankTransactionId)
            return Result.Conflict(DomainErrors.TopUpRequest_ExternalTransactionMismatch);
        if (kind == GatewayResultKinds.Unpaid && ExternalTransactionId is not null)
            return Result.Conflict(DomainErrors.TopUpRequest_GatewayUnpaidWithBankTransaction);

        GatewayEventId = eventId;
        GatewayName = gateway;
        GatewayResultKind = kind;
        GatewayResultCode = resultCode;
        GatewayResultAtUtc = occurredAtUtc;
        GatewayOriginalAmountRial = originalAmountRial;
        GatewayAffectiveAmountRial = affectiveAmountRial;
        if (bankTransactionId is not null && kind != GatewayResultKinds.Unknown)
            ExternalTransactionId = bankTransactionId;
        if (bankReferenceNumber is not null)
            BankReferenceNumber = bankReferenceNumber;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
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

    public void CancelGateway(string gatewayName, string reason)
    {
        if (PaymentMethod != PaymentMethod.Gateway)
            throw new DomainException(DomainErrors.TopUpRequest_PaymentMethodNotSupported);

        Reject(reason);
        GatewayName = gatewayName;
    }
}
