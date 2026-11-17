using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Modules.Purchases;

public class PhysicalProductPurchase : BaseEntity<PhysicalProductPurchaseId>
{
    public Guid CartId { get; private set; }
    public UserId BuyerId { get; private set; }
    public string ExternalProductId { get; private set; } = null!;
    public string ProductTitle { get; private set; } = null!;

    /// <summary>عوامل مرسولهٔ فریزشدهٔ همین قلم؛ برای خرید قدیمی بدون این عوامل null است.</summary>
    public ParcelFacts? Parcel { get; private set; }

    public bool HasParcelFacts => Parcel is { IsComplete: true };

    public int Quantity { get; private set; }
    public decimal UnitPriceInNoor { get; private set; }
    public decimal PaidAmountInNoor { get; private set; }

    /// <summary>سهم نور عادی پرداخت واقعی این قلم؛ برای خریدهای پیش از ثبت سهم null است.</summary>
    public decimal? RegularAmountInNoor { get; private set; }

    /// <summary>سهم نور هدیهٔ پرداخت واقعی این قلم؛ برای خریدهای پیش از ثبت سهم null است.</summary>
    public decimal? GiftAmountInNoor { get; private set; }
    public Guid? LedgerTransactionId { get; private set; }
    public PhysicalDeliveryStatus DeliveryStatus { get; private set; } = PhysicalDeliveryStatus.Preparing;
    public DateTime? ShippedAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }

    /// <summary>
    /// زمان تشخیص برگشتی از واسط پستی؛ وضعیت تحویل عوض نمی‌شود و اثر مالی ندارد.
    /// </summary>
    public DateTime? CarrierReturnedAtUtc { get; private set; }

    /// <summary>کد رهگیری مرسوله که به خریدار نشان داده می‌شود؛ تا پیش از «تحویل‌شده» قابل اصلاح است.</summary>
    public string? TrackingCode { get; private set; }

    /// <summary>منبع کد رهگیری: <c>PostEx</c> برای استعلام خودکار وضعیت، <c>Manual</c> برای ثبت دستی ادمین.</summary>
    public string? TrackingProvider { get; private set; }

    /// <summary>شناسهٔ ارجاع مرسوله نزد واسط پستی برای استعلام وضعیت؛ فقط برای منابع خودکار پر می‌شود.</summary>
    public string? ProviderParcelId { get; private set; }

    public const int TrackingCodeMaxLength = 50;
    public const int TrackingProviderMaxLength = 20;
    public const int ProviderParcelIdMaxLength = 50;
    public const string PostexProvider = "PostEx";
    public const string ManualProvider = "Manual";

    /// <summary>تنها فهرست منابع مجاز کد رهگیری؛ دامنه، اعتبارسنج و قید دیتابیس از همین می‌خوانند.</summary>
    public static IReadOnlyList<string> AllowedTrackingProviders { get; } = [PostexProvider, ManualProvider];

    /// <summary>جمع تعداد لغوشدهٔ این قلم در همهٔ عملیات لغو.</summary>
    public int CancelledQuantity { get; private set; }

    /// <summary>جمع نور عادی برگشتی این قلم؛ هرگز از سهم عادی پرداخت بیشتر نمی‌شود (D09).</summary>
    public decimal RefundedRegularInNoor { get; private set; }

    /// <summary>جمع نور هدیهٔ برگشتی این قلم؛ هرگز از سهم هدیهٔ پرداخت بیشتر نمی‌شود (D09).</summary>
    public decimal RefundedGiftInNoor { get; private set; }

    public int RemainingQuantity => Quantity - CancelledQuantity;

    public bool HasPaymentShares => RegularAmountInNoor.HasValue && GiftAmountInNoor.HasValue;

    private PhysicalProductPurchase() { }

    public static PhysicalProductPurchase Create(
        PhysicalProductPurchaseId id,
        Guid cartId,
        UserId buyerId,
        string externalProductId,
        string productTitle,
        int quantity,
        decimal unitPriceInNoor,
        decimal paidAmountInNoor,
        ParcelFacts? parcel = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalProductId);
        ArgumentException.ThrowIfNullOrWhiteSpace(productTitle);
        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }
        if (unitPriceInNoor < 0 || decimal.Truncate(unitPriceInNoor) != unitPriceInNoor)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPriceInNoor));
        }
        if (paidAmountInNoor < 0 || decimal.Truncate(paidAmountInNoor) != paidAmountInNoor)
        {
            throw new ArgumentOutOfRangeException(nameof(paidAmountInNoor));
        }
        if (parcel is not null && !parcel.IsComplete)
        {
            throw new ArgumentException("Parcel facts must be complete.", nameof(parcel));
        }

        return new PhysicalProductPurchase
        {
            Id = id,
            CartId = cartId,
            BuyerId = buyerId,
            ExternalProductId = externalProductId.Trim(),
            ProductTitle = productTitle.Trim(),
            Parcel = parcel,
            Quantity = quantity,
            UnitPriceInNoor = unitPriceInNoor,
            PaidAmountInNoor = paidAmountInNoor
        };
    }

    public void RecordPaymentShares(PaymentShares shares)
    {
        shares.EnsureMatches(PaidAmountInNoor);
        RegularAmountInNoor = shares.RegularInNoor;
        GiftAmountInNoor = shares.GiftInNoor;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFree(Guid? referenceId = null)
    {
        if (PaidAmountInNoor > 0)
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_CannotMarkNonFreeAsFree);
        }

        if (referenceId.HasValue && referenceId.Value == Guid.Empty)
        {
            throw new DomainException(DomainErrors.CoursePurchase_InvalidLedgerTransactionId);
        }

        LedgerTransactionId = referenceId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDebited(Guid ledgerTransactionId)
    {
        LedgerTransactionId = ledgerTransactionId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkShipped(
        string? trackingCode = null, string? trackingProvider = null, string? providerParcelId = null)
    {
        if (DeliveryStatus == PhysicalDeliveryStatus.Delivered)
        {
            // پس از تحویل، کد رهگیری و وضعیت قفل است.
            throw InvalidDeliveryTransition(PhysicalDeliveryStatus.Shipped);
        }

        var code = NormalizeTrackingCode(trackingCode);
        var provider = NormalizeTrackingProvider(trackingProvider);

        if (DeliveryStatus == PhysicalDeliveryStatus.Shipped)
        {
            // ارسال دوبارهٔ Shipped فقط کد و منبع را اصلاح می‌کند؛ زمان ارسال و بقیهٔ داده دست نمی‌خورد.
            TrackingCode = trackingCode is null ? TrackingCode : code;
            TrackingProvider = trackingProvider is null ? TrackingProvider : provider;
            ProviderParcelId = provider is null
                ? ProviderParcelId
                : NormalizeProviderParcelId(provider, providerParcelId);
            UpdatedAt = DateTime.UtcNow;
            return;
        }

        // قلمی که همهٔ تعدادش لغو شده، چیزی برای ارسال ندارد.
        if (DeliveryStatus != PhysicalDeliveryStatus.Preparing || RemainingQuantity == 0)
        {
            throw InvalidDeliveryTransition(PhysicalDeliveryStatus.Shipped);
        }

        var now = DateTime.UtcNow;
        DeliveryStatus = PhysicalDeliveryStatus.Shipped;
        ShippedAtUtc = now;
        TrackingCode = code;
        TrackingProvider = provider;
        ProviderParcelId = NormalizeProviderParcelId(provider, providerParcelId);
        UpdatedAt = now;
    }

    public void MarkDelivered()
    {
        if (DeliveryStatus == PhysicalDeliveryStatus.Delivered)
        {
            return;
        }

        if (DeliveryStatus != PhysicalDeliveryStatus.Shipped)
        {
            throw InvalidDeliveryTransition(PhysicalDeliveryStatus.Delivered);
        }

        var now = DateTime.UtcNow;
        DeliveryStatus = PhysicalDeliveryStatus.Delivered;
        DeliveredAtUtc = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// ثبت یک‌بارهٔ برگشتی واسط پستی بدون تغییر وضعیت تحویل و بدون اثر Ledger.
    /// </summary>
    public void MarkCarrierReturned(DateTime detectedAtUtc)
    {
        if (CarrierReturnedAtUtc.HasValue)
        {
            return;
        }

        CarrierReturnedAtUtc = detectedAtUtc;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// ثبت لغو تعدادی از این قلم پیش از ارسال؛ سقف برگشت «سهم پرداخت واقعی منهای برگشت‌های قبلی» است.
    /// </summary>
    public void RecordCancellation(int quantity, PaymentShares refund)
    {
        var exceedsShares = !HasPaymentShares ||
            RefundedRegularInNoor + refund.RegularInNoor > RegularAmountInNoor!.Value ||
            RefundedGiftInNoor + refund.GiftInNoor > GiftAmountInNoor!.Value;

        if (DeliveryStatus != PhysicalDeliveryStatus.Preparing ||
            quantity < 1 ||
            quantity > RemainingQuantity ||
            refund.RegularInNoor < 0 ||
            refund.GiftInNoor < 0 ||
            exceedsShares)
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_CancellationInvalid);
        }

        CancelledQuantity += quantity;
        RefundedRegularInNoor += refund.RegularInNoor;
        RefundedGiftInNoor += refund.GiftInNoor;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string? NormalizeTrackingCode(string? trackingCode)
    {
        if (trackingCode is null)
        {
            return null;
        }

        var trimmed = trackingCode.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > TrackingCodeMaxLength || trimmed.Any(char.IsControl))
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_InvalidTrackingCode);
        }

        return trimmed;
    }

    private static string? NormalizeTrackingProvider(string? trackingProvider)
    {
        if (string.IsNullOrWhiteSpace(trackingProvider))
        {
            return null;
        }

        var trimmed = trackingProvider.Trim();
        var allowed = AllowedTrackingProviders.FirstOrDefault(
            provider => string.Equals(provider, trimmed, StringComparison.OrdinalIgnoreCase));
        if (allowed is null)
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_InvalidTrackingProvider);
        }

        return allowed;
    }

    /// <summary>فقط ثبت دستی شناسهٔ مرسوله را خالی می‌کند؛ منبع خودکار آن را نگه می‌دارد.</summary>
    private static string? NormalizeProviderParcelId(string? provider, string? providerParcelId)
    {
        if (provider is null || IsManual(provider))
        {
            return null;
        }

        if (providerParcelId is null)
        {
            return null;
        }

        var trimmed = providerParcelId.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > ProviderParcelIdMaxLength || trimmed.Any(char.IsControl))
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_InvalidProviderParcelId);
        }

        return trimmed;
    }

    private static bool IsManual(string? provider) =>
        string.Equals(provider, ManualProvider, StringComparison.Ordinal);

    private ConflictDomainException InvalidDeliveryTransition(PhysicalDeliveryStatus target) =>
        new(string.Format(DomainErrors.PhysicalProductPurchase_InvalidDeliveryTransition, DeliveryStatus, target));
}
