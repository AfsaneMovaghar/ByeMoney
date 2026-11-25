using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet;
using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Modules.Purchases;

public class PhysicalProductPurchase : BaseEntity<PhysicalProductPurchaseId>
{
    public Guid CartId { get; private set; }
    public string ScopeId { get; private set; } = null!;
    public string SellerId { get; private set; } = null!;
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

    /// <summary>تصمیم و تعیین‌تکلیف ادمین برای مرسولهٔ برگشتی.</summary>
    public CarrierReturnResolution? CarrierReturnResolution { get; private set; }

    /// <summary>یادداشت ادمین دربارهٔ نحوهٔ رسیدگی به مرسولهٔ برگشتی.</summary>
    public string? CarrierReturnResolutionNote { get; private set; }

    /// <summary>زمان تعیین‌تکلیف مرسولهٔ برگشتی توسط ادمین.</summary>
    public DateTime? CarrierReturnResolvedAtUtc { get; private set; }

    /// <summary>شناسهٔ داخلی ادمین تعیین‌تکلیف‌کننده.</summary>
    public Guid? CarrierReturnResolvedByUserId { get; private set; }

    /// <summary>کد وضعیت خسارت (۱۱۳۰۴) یا مفقودی (۱۱۳۰۵). وضعیت تحویل را عوض نمی‌کند.</summary>
    public int? CarrierAlertCode { get; private set; }

    /// <summary>زمان ثبت هشدار خسارت یا مفقودی.</summary>
    public DateTime? CarrierAlertAtUtc { get; private set; }

    /// <summary>تعیین‌تکلیف ادمین برای هشدار خسارت یا مفقودی؛ جدا از برگشتی.</summary>
    public CarrierReturnResolution? CarrierAlertResolution { get; private set; }

    /// <summary>یادداشت ادمین برای تعیین‌تکلیف هشدار.</summary>
    public string? CarrierAlertResolutionNote { get; private set; }

    /// <summary>زمان تعیین‌تکلیف هشدار.</summary>
    public DateTime? CarrierAlertResolvedAtUtc { get; private set; }

    /// <summary>شناسهٔ داخلی ادمین تعیین‌تکلیف‌کنندهٔ هشدار.</summary>
    public Guid? CarrierAlertResolvedByUserId { get; private set; }

    /// <summary>نشانی مطلق https رهگیری، اگر واسط آن را داده باشد. از روی کد ساخته نمی‌شود.</summary>
    public string? TrackingUrl { get; private set; }

    public const int CarrierReturnResolutionMaxLength = 30;
    public const int CarrierReturnResolutionNoteMaxLength = 1000;
    public const int TrackingUrlMaxLength = 1000;
    public const int DamagedParcelStatusCode = 11304;
    public const int MissingShipmentStatusCode = 11305;
    public const int RouteNotFoundStatusCode = 11310;
    public const int DeliveryRetryStatusCode = 11311;
    public const int OthersParentCode = 10;
    public const int CanceledParentCode = 8;

    /// <summary>مرسوله برگشتی دارد و هنوز توسط ادمین تعیین‌تکلیف نشده است.</summary>
    public bool HasUnresolvedCarrierReturn => CarrierReturnedAtUtc != null && CarrierReturnResolution == null;

    /// <summary>مرسوله برگشتی با تصمیم عدم تسویه بسته شده و برای همیشه از تسویه و تحویل خارج است.</summary>
    public bool HasClosedWithoutSettlementReturn => CarrierReturnedAtUtc != null && CarrierReturnResolution == Purchases.CarrierReturnResolution.ClosedWithoutSettlement;

    /// <summary>هشدار خسارت یا مفقودی هنوز تعیین‌تکلیف نشده است. تأیید خریدار آن را نمی‌بندد.</summary>
    public bool HasUnresolvedCarrierAlert => CarrierAlertCode != null && CarrierAlertResolution == null;

    /// <summary>
    /// هشدارِ حل‌نشده یا بسته‌شده بدون تسویه. تحویل خودکار و همگام این قلم را Delivered نمی‌کند.
    /// </summary>
    public bool ExcludedFromAutomaticDeliveryByAlert =>
        CarrierAlertCode != null && CarrierAlertResolution != Purchases.CarrierReturnResolution.DeliveryConfirmed;

    /// <summary>فقط دو کد خسارت و مفقودی هشدار پایدار می‌سازند.</summary>
    public static bool IsDurableCarrierAlertCode(int statusCode) =>
        statusCode is DamagedParcelStatusCode or MissingShipmentStatusCode;

    /// <summary>کد رهگیری مرسوله که به خریدار نشان داده می‌شود؛ تا پیش از «تحویل‌شده» قابل اصلاح است.</summary>
    public string? TrackingCode { get; private set; }

    /// <summary>منبع کد رهگیری: <c>PostEx</c> برای استعلام خودکار وضعیت، <c>Manual</c> برای ثبت دستی ادمین.</summary>
    public string? TrackingProvider { get; private set; }

    /// <summary>شناسهٔ ارجاع مرسوله نزد واسط پستی برای استعلام وضعیت؛ فقط برای منابع خودکار پر می‌شود.</summary>
    public string? ProviderParcelId { get; private set; }

    /// <summary>تعداد قلم در لحظهٔ ساخت مرسوله؛ نگهبان ارتقای خودکار آن را با تعداد باقی‌مانده می‌سنجد.</summary>
    public int? CarrierParcelQuantity { get; private set; }

    /// <summary>
    /// زمان تشخیص «در انتظار پرداخت» مرسولهٔ واسط. وضعیت تحویل را عوض نمی‌کند،
    /// چون نه ارسال شده و نه پول کرایه پرداخت شده است.
    /// </summary>
    public DateTime? CarrierPaymentPendingSinceUtc { get; private set; }

    /// <summary>
    /// زمان شروع آخرین تلاش ساخت مرسوله؛ رزرو کوتاه در دیتابیس تا دو نمونه هم‌زمان دو مرسوله نسازند.
    /// تا ثبت نتیجه یا انقضای مهلت پاک نمی‌شود.
    /// </summary>
    public DateTime? ShipmentRequestStartedAtUtc { get; private set; }

    /// <summary>مرسوله در واسط ساخته شده ولی کرایه‌اش پرداخت نشده است.</summary>
    public bool HasPendingCarrierPayment => CarrierPaymentPendingSinceUtc.HasValue;

    /// <summary>قلم شناسهٔ ارجاع مرسولهٔ واسط دارد.</summary>
    public bool HasCarrierParcel => !string.IsNullOrWhiteSpace(ProviderParcelId);

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
        string scopeId,
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
            ScopeId = FinancialScope.Require(scopeId),
            SellerId = FinancialScope.TarheElahi,
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
            // اصلاح کد فقط با بارکد واقعی؛ کد ستاره‌ای یا خالی کد ذخیره‌شده را عوض نمی‌کند.
            if (trackingCode is not null && !IsUsablePostalTrackingCode(code))
            {
                throw new DomainException(DomainErrors.PhysicalProductPurchase_TrackingCodeRequired);
            }

            // ارسال دوبارهٔ Shipped فقط کد و منبع را اصلاح می‌کند؛ زمان ارسال و بقیهٔ داده دست نمی‌خورد.
            TrackingCode = trackingCode is null ? TrackingCode : code;
            TrackingProvider = trackingProvider is null ? TrackingProvider : provider;
            ProviderParcelId = provider is null
                ? ProviderParcelId
                : NormalizeProviderParcelId(provider, providerParcelId);
            ShipmentRequestStartedAtUtc = null;
            CarrierPaymentPendingSinceUtc = null;
            if (IsManual(TrackingProvider))
            {
                TrackingUrl = null;
            }

            UpdatedAt = DateTime.UtcNow;
            return;
        }

        // قلمی که همهٔ تعدادش لغو شده، چیزی برای ارسال ندارد.
        if (DeliveryStatus != PhysicalDeliveryStatus.Preparing || RemainingQuantity == 0)
        {
            throw InvalidDeliveryTransition(PhysicalDeliveryStatus.Shipped);
        }

        if (!IsUsablePostalTrackingCode(code))
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_TrackingCodeRequired);
        }

        // شناسهٔ پستکس از قبل ذخیره شده؛ کد دستی همان مرسوله را تکمیل می‌کند و منبع را عوض نمی‌کند.
        if (!string.IsNullOrWhiteSpace(ProviderParcelId) && !IsManual(TrackingProvider))
        {
            provider = TrackingProvider;
            providerParcelId = ProviderParcelId;
        }

        var now = DateTime.UtcNow;
        DeliveryStatus = PhysicalDeliveryStatus.Shipped;
        ShippedAtUtc = now;
        TrackingCode = code;
        TrackingProvider = provider;
        ProviderParcelId = NormalizeProviderParcelId(provider, providerParcelId);
        ShipmentRequestStartedAtUtc = null;
        CarrierPaymentPendingSinceUtc = null;
        if (IsManual(TrackingProvider))
        {
            TrackingUrl = null;
        }

        UpdatedAt = now;
    }

    /// <summary>
    /// شناسهٔ ارجاع واسط را پیش از آمدن کد پستی نگه می‌دارد. وضعیت «آماده‌سازی» می‌ماند
    /// تا خواندن بعدی همان مرسوله، با کد واقعی، <see cref="MarkShipped"/> شود.
    /// تعداد همان لحظه هم ثبت می‌شود تا ارتقای خودکار با تعداد فعلی سنجیده شود.
    /// قلمی که در همین فاصله کامل لغو شده هم ارجاع می‌گیرد تا مرسولهٔ واسط گم نشود؛
    /// <see cref="MarkShipped"/> همچنان با تعداد صفر کار نمی‌کند.
    /// </summary>
    public void RememberCarrierParcel(string trackingProvider, string providerParcelId, int quantity)
    {
        if (DeliveryStatus != PhysicalDeliveryStatus.Preparing)
        {
            throw InvalidDeliveryTransition(PhysicalDeliveryStatus.Shipped);
        }

        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        var provider = NormalizeTrackingProvider(trackingProvider);
        var parcelId = NormalizeProviderParcelId(provider, providerParcelId);
        if (provider is null || parcelId is null)
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_InvalidProviderParcelId);
        }

        if (ProviderParcelId is not null)
        {
            return;
        }

        TrackingProvider = provider;
        ProviderParcelId = parcelId;
        CarrierParcelQuantity = quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// نشان «در انتظار پرداخت» را یک‌بار ثبت می‌کند؛ زمان اولین تشخیص نگه داشته می‌شود.
    /// </summary>
    public void MarkCarrierPaymentPending(DateTime utcNow)
    {
        if (DeliveryStatus != PhysicalDeliveryStatus.Preparing)
        {
            throw InvalidDeliveryTransition(PhysicalDeliveryStatus.Preparing);
        }

        if (ProviderParcelId is null)
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_InvalidProviderParcelId);
        }

        if (CarrierPaymentPendingSinceUtc.HasValue)
        {
            return;
        }

        CarrierPaymentPendingSinceUtc = utcNow;
        UpdatedAt = utcNow;
    }

    /// <summary>نشان «در انتظار پرداخت» با پذیرش مرسوله یا تعیین‌تکلیف آن پاک می‌شود.</summary>
    public void ClearCarrierPaymentPending()
    {
        if (!CarrierPaymentPendingSinceUtc.HasValue)
        {
            return;
        }

        CarrierPaymentPendingSinceUtc = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// رزرو کوتاه ساخت مرسوله. تا وقتی رزرو تازه است درخواست دوم رد می‌شود؛
    /// پس از انقضا تلاش بعدی آزاد است و خواندن پیش از ساخت، مرسولهٔ دوم نمی‌سازد.
    /// </summary>
    public bool TryBeginShipmentRequest(DateTime utcNow, TimeSpan lease)
    {
        if (DeliveryStatus != PhysicalDeliveryStatus.Preparing || RemainingQuantity == 0)
        {
            throw InvalidDeliveryTransition(PhysicalDeliveryStatus.Shipped);
        }

        if (ShipmentRequestStartedAtUtc is { } startedAt && startedAt > utcNow - lease)
        {
            return false;
        }

        ShipmentRequestStartedAtUtc = utcNow;
        UpdatedAt = utcNow;
        return true;
    }

    /// <summary>
    /// آزادکردن رزرو فقط وقتی درخواست پیش از ارسال رد شده یا پاسخ قطعی ناموفق بوده است؛
    /// در نتیجهٔ نامعلوم رزرو می‌ماند تا خودش منقضی شود.
    /// </summary>
    public void AbandonShipmentRequest()
    {
        if (!ShipmentRequestStartedAtUtc.HasValue)
        {
            return;
        }

        ShipmentRequestStartedAtUtc = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// پاک‌کردن ارجاع مرسوله وقتی واسط آن را نمی‌شناسد (مثلاً پشتیبانی حذف کرده است)،
    /// تا ساخت دوباره با همان کلید یکتا مجاز شود.
    /// </summary>
    public void ClearCarrierParcelReference()
    {
        if (DeliveryStatus != PhysicalDeliveryStatus.Preparing)
        {
            throw InvalidDeliveryTransition(PhysicalDeliveryStatus.Preparing);
        }

        ProviderParcelId = null;
        TrackingProvider = null;
        TrackingUrl = null;
        CarrierParcelQuantity = null;
        CarrierPaymentPendingSinceUtc = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// نشانی مطلق https را نگه می‌دارد. http، نشانی نسبی و منبع دستی نادیده گرفته می‌شوند.
    /// اگر نشانی عوض نشده باشد هیچ نوشتنی لازم نیست.
    /// </summary>
    public bool RememberTrackingUrl(string? url)
    {
        var normalized = NormalizeHttpsTrackingUrl(url);
        if (normalized is null || IsManual(TrackingProvider) || TrackingUrl == normalized)
        {
            return false;
        }

        TrackingUrl = normalized;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>فقط نشانی مطلق با طرح https پذیرفته می‌شود.</summary>
    public static string? NormalizeHttpsTrackingUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var trimmed = url.Trim();
        if (trimmed.Length > TrackingUrlMaxLength || trimmed.Any(char.IsControl))
        {
            return null;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed) ||
            !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed;
    }

    /// <summary>
    /// بارکد قابل‌استفاده نویسهٔ خالی یا دارای ستاره نیست. شمارهٔ داخلی مرسوله در این فیلد نمی‌آید.
    /// </summary>
    public static bool IsUsablePostalTrackingCode(string? tracking)
    {
        if (string.IsNullOrWhiteSpace(tracking))
        {
            return false;
        }

        var trimmed = tracking.Trim();
        return trimmed.Any(char.IsLetterOrDigit) && !trimmed.Contains('*');
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

        if (HasUnresolvedCarrierReturn || HasClosedWithoutSettlementReturn)
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_DeliveryBlockedByCarrierReturn);
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

        if (DeliveryStatus != PhysicalDeliveryStatus.Shipped)
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_CarrierReturnRequiresShipped);
        }

        CarrierReturnedAtUtc = detectedAtUtc;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// ثبت یک‌بارهٔ هشدار خسارت یا مفقودی، فقط روی قلم ارسال‌شده، بدون تغییر وضعیت و بدون Ledger.
    /// </summary>
    public void MarkCarrierAlert(int statusCode, DateTime detectedAtUtc)
    {
        if (CarrierAlertCode.HasValue)
        {
            return;
        }

        if (!IsDurableCarrierAlertCode(statusCode))
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_InvalidCarrierAlertCode);
        }

        if (DeliveryStatus != PhysicalDeliveryStatus.Shipped)
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_CarrierAlertRequiresShipped);
        }

        CarrierAlertCode = statusCode;
        CarrierAlertAtUtc = detectedAtUtc;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// تعیین‌تکلیف هشدار خسارت یا مفقودی. تأیید خریدار این هشدار را نمی‌بندد.
    /// <see cref="CarrierReturnResolution.DeliveryConfirmed"/> اگر قلم هنوز ارسال‌شده باشد آن را تحویل‌شده می‌کند.
    /// <see cref="CarrierReturnResolution.ClosedWithoutSettlement"/> وضعیت را به عقب برنمی‌گرداند.
    /// </summary>
    public void ResolveCarrierAlert(CarrierReturnResolution resolution, string note, Guid adminUserId, DateTime utcNow)
    {
        if (!CarrierAlertCode.HasValue)
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_NotCarrierAlert);
        }

        if (string.IsNullOrWhiteSpace(note) || note.Length > CarrierReturnResolutionNoteMaxLength)
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_InvalidCarrierAlertResolutionNote);
        }

        if (CarrierAlertResolution.HasValue)
        {
            if (CarrierAlertResolution == resolution && CarrierAlertResolutionNote == note)
            {
                return;
            }

            throw new ConflictDomainException(DomainErrors.PhysicalProductPurchase_CarrierAlertResolutionConflict);
        }

        CarrierAlertResolution = resolution;
        CarrierAlertResolutionNote = note;
        CarrierAlertResolvedAtUtc = utcNow;
        CarrierAlertResolvedByUserId = adminUserId;
        UpdatedAt = utcNow;

        if (resolution == Purchases.CarrierReturnResolution.DeliveryConfirmed &&
            DeliveryStatus == PhysicalDeliveryStatus.Shipped)
        {
            DeliveryStatus = PhysicalDeliveryStatus.Delivered;
            DeliveredAtUtc ??= utcNow;
        }
    }

    /// <summary>
    /// تعیین‌تکلیف مرسولهٔ برگشتی توسط ادمین (D10).
    /// </summary>
    public void ResolveCarrierReturn(CarrierReturnResolution resolution, string note, Guid adminUserId, DateTime utcNow)
    {
        if (!CarrierReturnedAtUtc.HasValue)
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_NotCarrierReturned);
        }

        if (string.IsNullOrWhiteSpace(note) || note.Length > CarrierReturnResolutionNoteMaxLength)
        {
            throw new DomainException(DomainErrors.PhysicalProductPurchase_InvalidCarrierReturnResolutionNote);
        }

        if (CarrierReturnResolution.HasValue)
        {
            if (CarrierReturnResolution == resolution && CarrierReturnResolutionNote == note)
            {
                return;
            }

            throw new ConflictDomainException(DomainErrors.PhysicalProductPurchase_CarrierReturnResolutionConflict);
        }

        CarrierReturnResolution = resolution;
        CarrierReturnResolutionNote = note;
        CarrierReturnResolvedAtUtc = utcNow;
        CarrierReturnResolvedByUserId = adminUserId;
        UpdatedAt = utcNow;

        if (resolution == Purchases.CarrierReturnResolution.DeliveryConfirmed)
        {
            DeliveryStatus = PhysicalDeliveryStatus.Delivered;
            DeliveredAtUtc ??= utcNow;
        }
        else if (resolution == Purchases.CarrierReturnResolution.ClosedWithoutSettlement)
        {
            DeliveryStatus = PhysicalDeliveryStatus.Shipped;
        }
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
