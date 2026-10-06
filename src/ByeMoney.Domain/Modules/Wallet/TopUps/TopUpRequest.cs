using System.Security.Cryptography;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Modules.Wallet.TopUps;

public class TopUpRequest : BaseEntity<TopUpRequestId>
{
    private static readonly char[] Base32Chars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ".ToCharArray();

    /// <summary>شناسه کاربری که شارژ به کیف پول او تعلق دارد؛ برای مالکیت درخواست و جست‌وجوی شارژهای کاربر ذخیره می‌شود.</summary>
    public UserId UserId { get; private set; }
    /// <summary>شناسه ادمینی که شارژ کارت‌به‌کارت را به‌جای کاربر ثبت کرده است؛ برای انتساب عملیات مدیریتی نگه‌داری می‌شود.</summary>
    public UserId? CreatedByUserId { get; private set; }
    /// <summary>نوع سناریوی شارژ، مانند شارژ ثبت‌شده توسط ادمین؛ برای تشخیص مسیر ایجاد درخواست ذخیره می‌شود.</summary>
    public ChargeType? ChargeType { get; private set; }
    /// <summary>مبلغ ریالی پرداخت؛ برای تطبیق پرداخت و محاسبه مبلغ نور ثبت می‌شود.</summary>
    public decimal? AmountRial { get; private set; }
    /// <summary>نرخ ریال به نور در زمان ایجاد شارژ؛ snapshot آن محاسبه تاریخی را در برابر تغییر نرخ حفظ می‌کند.</summary>
    public decimal? RialPerNoorSnapshot { get; private set; }
    /// <summary>شناسه رسید پرداخت کاربر؛ برای پیگیری و جلوگیری از ثبت تکراری رسید نگه‌داری می‌شود.</summary>
    public string? ReceiptId { get; private set; }
    /// <summary>کلید یکتای درخواست ایجاد؛ برای جلوگیری از ثبت دوباره یک عملیات در retryها استفاده می‌شود.</summary>
    public string? IdempotencyKey { get; private set; }
    /// <summary>مقدار نور شارژ؛ پس از تأیید برای ثبت اعتبار کیف پول مبنا قرار می‌گیرد.</summary>
    public decimal AmountNoor { get; private set; }
    /// <summary>روش پرداخت، مانند کارت‌به‌کارت یا درگاه؛ برای انتخاب قواعد پیگیری و تأیید استفاده می‌شود.</summary>
    public PaymentMethod PaymentMethod { get; private set; }
    /// <summary>کد مرجع قابل نمایش به کاربر؛ برای اتصال نتیجه بازگشت از درگاه به درخواست شارژ استفاده می‌شود.</summary>
    public string ClientReferenceCode { get; private set; } = null!;
    /// <summary>وضعیت چرخه عمر شارژ؛ برای کنترل انتقال وضعیت، نمایش و جلوگیری از تأیید یا رد ناسازگار ذخیره می‌شود.</summary>
    public TopUpStatus Status { get; private set; }
    /// <summary>شناسه تراکنش نزد بانک/درگاه؛ برای تطبیق پرداخت خارجی و جلوگیری از اتصال یک تراکنش به چند شارژ به‌کار می‌رود.</summary>
    public string? ExternalTransactionId { get; private set; }
    /// <summary>نام درگاه گزارش‌دهنده نتیجه؛ برای ردیابی منبع پاسخ درگاه ذخیره می‌شود.</summary>
    public string? GatewayName { get; private set; }
    /// <summary>شماره مرجع بانکی پرداخت؛ برای تطبیق نتیجه درگاه با گزارش بانکی نگه‌داری می‌شود.</summary>
    public string? BankReferenceNumber { get; private set; }
    /// <summary>کد خام نتیجه اعلام‌شده از درگاه؛ برای عیب‌یابی و تطبیق پاسخ ذخیره می‌شود.</summary>
    public string? GatewayResultCode { get; private set; }
    /// <summary>دسته‌بندی نتیجه درگاه (موفق، ناموفق یا نامشخص)؛ مبنای پردازش نتیجه و بررسی مغایرت است.</summary>
    public string? GatewayResultKind { get; private set; }
    /// <summary>شناسه رویداد نتیجه درگاه؛ برای تشخیص تکرار رویداد و کنترل هم‌زمانی نگه‌داری می‌شود.</summary>
    public string? GatewayEventId { get; private set; }
    /// <summary>زمان وقوع نتیجه نزد درگاه؛ برای ثبت زمان گزارش‌شده مستقل از زمان دریافت در سامانه استفاده می‌شود.</summary>
    public DateTime? GatewayResultAtUtc { get; private set; }
    /// <summary>مبلغ اصلی ریالی گزارش‌شده توسط درگاه؛ برای ثبت شواهد و تطبیق پرداخت نگه‌داری می‌شود.</summary>
    public decimal? GatewayOriginalAmountRial { get; private set; }
    /// <summary>مبلغ مؤثر ریالی گزارش‌شده توسط درگاه؛ برای بررسی مبلغی که نتیجه پرداخت بر اساس آن اعلام شده ذخیره می‌شود.</summary>
    public decimal? GatewayAffectiveAmountRial { get; private set; }
    /// <summary>علت رد یا لغو شارژ؛ برای توضیح تصمیم و پیگیری پشتیبانی ثبت می‌شود.</summary>
    public string? RejectionReason { get; private set; }
    /// <summary>زمان تأیید شارژ؛ برای تاریخچه وضعیت و گزارش‌گیری ثبت می‌شود.</summary>
    public DateTime? ConfirmedAtUtc { get; private set; }
    /// <summary>زمان رد شارژ؛ برای تاریخچه وضعیت و گزارش‌گیری ثبت می‌شود.</summary>
    public DateTime? RejectedAtUtc { get; private set; }
    /// <summary>شناسه پرونده بررسی مالی؛ برای اتصال گزارش/پیگیری بیرونی به همین شارژ و جلوگیری از پرونده تکراری ذخیره می‌شود.</summary>
    public string? ReviewCaseId { get; private set; }
    /// <summary>کد علت بازشدن بررسی مالی؛ برای دسته‌بندی و پردازش قابل اتکای پرونده نگه‌داری می‌شود.</summary>
    public string? ReviewReasonCode { get; private set; }
    /// <summary>زمان شروع بررسی مالی؛ برای ثبت چرخه رسیدگی ذخیره می‌شود.</summary>
    public DateTime? ReviewOpenedAtUtc { get; private set; }
    /// <summary>زمان پایان بررسی مالی؛ خالی بودن آن نشان می‌دهد رسیدگی هنوز حل نشده است.</summary>
    public DateTime? ReviewResolvedAtUtc { get; private set; }
    /// <summary>کد نتیجه نهایی بررسی؛ برای بیان نتیجه ماشین‌خوان پرونده نگه‌داری می‌شود.</summary>
    public string? ReviewOutcomeCode { get; private set; }
    /// <summary>مرجع مالی اقدام نهایی بررسی؛ برای تطبیق نتیجه با ثبت یا اصلاح مالی مربوط ذخیره می‌شود.</summary>
    public string? ReviewResolutionFinancialReferenceId { get; private set; }
    /// <summary>شماره بازبینی پرونده؛ برای تشخیص تغییرات هم‌زمان و کنترل ترتیب تصمیم‌ها استفاده می‌شود.</summary>
    public int ReviewRevision { get; private set; }
    /// <summary>مرجع بازپرداخت دستی؛ برای پیگیری بازپرداختی که بیرون از جریان خودکار انجام شده ثبت می‌شود.</summary>
    public string? ManualRefundReference { get; private set; }
    /// <summary>فهرست رویدادهای رسیدگی و شواهد آن‌ها؛ به‌صورت JSONB برای ممیزی تاریخچه تصمیم‌های مالی ذخیره می‌شود.</summary>
    public IReadOnlyList<FinancialReviewAudit> ReviewAudit { get; private set; } = [];

    /// <summary>اقلام خرید موقت متصل به شارژ و snapshot نوع، شناسه و قیمت هر قلم؛ برای ادامه خرید پس از تأیید پرداخت ذخیره می‌شود.</summary>
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
                outcomeCode == FinancialReviewOutcomeCodes.NoMatchingDeposit && ManualRefundReference is not null ||
                outcomeCode == FinancialReviewOutcomeCodes.NoMatchingDeposit && evidence.MatchingDepositFound != false ||
                outcomeCode == FinancialReviewOutcomeCodes.ManualRefund && string.IsNullOrWhiteSpace(evidence.ManualRefundReference))
                return Result.Conflict(DomainErrors.TopUpRequest_ReviewStateMismatch);
            ReviewOutcomeCode = outcomeCode;
            ReviewResolutionFinancialReferenceId = financialReferenceId;
            ReviewResolvedAtUtc = DateTime.UtcNow;
            if (outcomeCode == FinancialReviewOutcomeCodes.ManualRefund)
            {
                ManualRefundReference = evidence.ManualRefundReference;
                Status = TopUpStatus.ManuallyRefunded;
            }
            else
                Status = TopUpStatus.Unresolved;
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
        if (Status == TopUpStatus.Unresolved)
            Status = TopUpStatus.Pending;
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
        decimal amountNoor,
        PaymentMethod paymentMethod,
        string? externalTransactionId = null,
        string? clientReferenceCode = null,
        IReadOnlyList<PendingItemSnapshot>? pendingItems = null,
        decimal? rialPerNoor = null)
    {
        if (amountNoor <= 0)
            throw new DomainException(DomainErrors.TopUpRequest_AmountMustBeGreaterThanZero);
        if (paymentMethod == PaymentMethod.Gateway && decimal.Truncate(amountNoor) != amountNoor)
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
            AmountNoor = amountNoor,
            AmountRial = rialPerNoor is null ? null : checked(amountNoor * rialPerNoor.Value),
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
        if (Status is TopUpStatus.Unresolved or TopUpStatus.ManuallyRefunded)
            return Result.Conflict(DomainErrors.TopUpRequest_ReviewStateMismatch);
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
            AmountNoor = noorAmount,
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
