using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Domain.Modules.Purchases;

public sealed class Cart
{
    public Guid Id { get; private set; }
    public UserId BuyerId { get; private set; }
    public string BuyerExternalUserId { get; private set; } = null!;

    /// <summary>
    /// اثر انگشت اقلام درخواستی. پیش از دریافت قیمت ردیفی وجود ندارد و همین آرایه تنها سند درخواست
    /// خریدار است؛ پس از قیمت‌گذاری، ردیف‌ها مرجع شناسه و مبلغ‌اند.
    /// </summary>
    public string[] RequestedItemSignatures { get; private set; } = [];

    public string? CouponCode { get; private set; }
    private readonly List<CartItem> _items = [];

    /// <summary>
    /// ترتیب قطعی (شناسه و سپس نوع) تا تخصیص مبلغ به اقلام قابل بازتولید بماند.
    /// </summary>
    public IReadOnlyList<CartItem> Items => _items
        .OrderBy(x => x.ExternalItemId, StringComparer.Ordinal)
        .ThenBy(x => x.ItemType)
        .ToArray();

    public decimal ItemDiscountInNoor { get; private set; }
    public decimal CouponDiscountInNoor { get; private set; }
    public decimal ShippingAmountInNoor { get; private set; }
    public string? ShippingRuleLabel { get; private set; }

    /// <summary>
    /// شناسهٔ پایدار مبدأ ارسال (مشترک با استرپی، مثل <c>ramsar</c>)؛ فقط برای سبد دارای کالا فریز می‌شود.
    /// </summary>
    public string? ShippingOriginId { get; private set; }

    public const int ShippingOriginIdMaxLength = 50;

    /// <summary>
    /// آدرس تحویل ثابت‌شدهٔ همین قیمت‌گذاری؛ فقط برای سبد دارای کالا پر می‌شود و با بازگشایی سبد پاک می‌شود.
    /// </summary>
    public ShippingAddress? ShippingAddress { get; private set; }
    public CartStatus Status { get; private set; }
    public Guid? TopUpRequestId { get; private set; }
    public Guid ReservationId { get; private set; }
    public bool Synchronized { get; private set; }
    public int SyncAttempts { get; private set; }
    public DateTime? LastSyncAttemptAtUtc { get; private set; }
    public string? LastSyncErrorCode { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// شناسهٔ کاربر ادمینی که این سبد را به‌نام خریدار ثبت کرده است؛ برای سبد عادی خریدار خالی است.
    /// </summary>
    public Guid? CreatedByAdminUserId { get; private set; }

    /// <summary>
    /// علت ثبت رایگان ادمین. وقتی پر است، قیمت quote فقط نمایشی است و تخصیص همهٔ اقلام، ارسال و جمع سبد صفرند.
    /// </summary>
    public string? AdminFreeReason { get; private set; }

    public bool IsAdminFree => AdminFreeReason is not null;

    public decimal Total => IsAdminFree
        ? 0m
        : Items.Sum(x => x.LineTotalInNoor) - ItemDiscountInNoor - CouponDiscountInNoor + ShippingAmountInNoor;
    public decimal OriginalTotal => Items.Sum(x => x.LineTotalInNoor);

    private const decimal MaximumOriginalTotalInNoor = 99999999999999m;

    /// <summary>
    /// هم‌اندازهٔ علت رایگان خرید دوره، چون همین متن روی خرید هر قلم رایگان هم ثبت می‌شود.
    /// </summary>
    public const int AdminFreeReasonMaxLength = 1000;

    private Cart()
    {
    }

    public static Cart Create(Guid id, Guid buyerId, string externalUserId, CartItemInput[] items, string? couponCode) => new()
    {
        Id = id,
        ReservationId = id,
        BuyerId = new UserId(buyerId),
        BuyerExternalUserId = externalUserId,
        RequestedItemSignatures = items
            .Select(x => CartItemSignature.Format(x.ExternalId, x.Type, x.Quantity))
            .Order(StringComparer.Ordinal)
            .ToArray(),
        CouponCode = couponCode?.Trim().ToUpperInvariant(),
        Status = CartStatus.Preparing,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
    };

    /// <summary>
    /// سبد سفارش دستی ادمین. نشانه‌های ادمین فقط هنگام ساخت ثبت می‌شوند تا تکرار همان شناسهٔ سبد
    /// با حالت متفاوت به سبد موجود نچسبد. سفارش ادمین کوپن نمی‌پذیرد.
    /// </summary>
    public static Cart CreateAdminOrder(
        Guid id,
        Guid buyerId,
        string externalUserId,
        CartItemInput[] items,
        Guid adminUserId,
        string? freeReason)
    {
        if (adminUserId == Guid.Empty)
        {
            throw new ArgumentException("Admin user id is required.", nameof(adminUserId));
        }

        if (freeReason is not null &&
            (string.IsNullOrWhiteSpace(freeReason) || freeReason.Trim().Length > AdminFreeReasonMaxLength))
        {
            throw new ArgumentException("Free reason must be non-blank and within the allowed length.", nameof(freeReason));
        }

        var cart = Create(id, buyerId, externalUserId, items, couponCode: null);
        cart.CreatedByAdminUserId = adminUserId;
        cart.AdminFreeReason = freeReason?.Trim();
        return cart;
    }

    public bool HasSameAdminOrder(Cart other) =>
        CreatedByAdminUserId == other.CreatedByAdminUserId &&
        AdminFreeReason == other.AdminFreeReason;

    public static Cart Create(Guid id, Guid buyerId, string externalUserId, string[] ids, string? couponCode) =>
        Create(id, buyerId, externalUserId, ids.Select(x => new CartItemInput(x, ProductItemType.Course, 1)).ToArray(), couponCode);

    public bool Matches(CartItemInput[] items, string? couponCode) =>
        RequestedItemSignatures.SequenceEqual(items
            .Select(x => CartItemSignature.Format(x.ExternalId, x.Type, x.Quantity))
            .Order(StringComparer.Ordinal)) &&
        CouponCode == couponCode?.Trim().ToUpperInvariant();

    public bool Matches(string[] ids, string? couponCode) =>
        RequestedItemSignatures.SequenceEqual(ids.Order(StringComparer.Ordinal)) &&
        CouponCode == couponCode?.Trim().ToUpperInvariant();

    public void SetQuote(
        IReadOnlyList<CartQuoteItem> items,
        decimal shippingAmountInNoor = 0m,
        string? shippingRuleLabel = null,
        ShippingAddress? shippingAddress = null,
        string? shippingOriginId = null)
    {
        if (Status != CartStatus.Preparing)
        {
            return;
        }

        if (items.Count != RequestedItemSignatures.Length ||
            items.Sum(x => x.LineTotalInNoor) > MaximumOriginalTotalInNoor ||
            !IsWholeNonNegativeNoor(shippingAmountInNoor) ||
            items.Any(HasInvalidPrice) ||
            items.GroupBy(x => (x.ExternalItemId, x.Type)).Any(g => g.Count() > 1) ||
            items.Any(x => !Enum.IsDefined(x.Type) || !ProductParentRule.IsValid(x.ExternalItemId, x.ParentExternalId)))
        {
            throw new ArgumentException(nameof(items));
        }

        var signatures = items
            .Select(x => CartItemSignature.Format(x.ExternalItemId, x.Type, x.Quantity))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!signatures.SequenceEqual(RequestedItemSignatures))
        {
            throw new ArgumentException(nameof(items));
        }

        var hasProduct = items.Any(x => x.Type == ProductItemType.Product);
        var normalizedOriginId = string.IsNullOrWhiteSpace(shippingOriginId) ? null : shippingOriginId.Trim();
        if (normalizedOriginId is not null && normalizedOriginId.Length > ShippingOriginIdMaxLength)
        {
            throw new ArgumentException(nameof(shippingOriginId));
        }

        _items.Clear();
        _items.AddRange(items.Select(x => new CartItem(
            x.ExternalItemId,
            x.Title,
            x.BasePriceInNoor,
            IsAdminFree ? 0m : x.PriceInNoor,
            x.Type,
            x.ParentExternalId,
            x.Quantity)));
        ItemDiscountInNoor = items.Sum(x => x.ItemDiscountInNoor);
        CouponDiscountInNoor = items.Sum(x => x.CouponDiscountInNoor);
        ShippingAmountInNoor = IsAdminFree ? 0m : shippingAmountInNoor;
        ShippingRuleLabel = string.IsNullOrWhiteSpace(shippingRuleLabel) ? null : shippingRuleLabel.Trim();
        ShippingAddress = hasProduct ? shippingAddress : null;
        // الزام مبدأ در prepare زنده (CartPricingClient) و ساخت مرسوله است؛ اینجا فقط فریز می‌شود.
        ShippingOriginId = hasProduct ? normalizedOriginId : null;

        if (_items.Sum(x => x.AllocatedAmountInNoor) + ShippingAmountInNoor != Total)
        {
            throw new ArgumentException("Allocations plus shipping must match cart total.");
        }

        Status = CartStatus.Ready;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static bool HasInvalidPrice(CartQuoteItem item) =>
        string.IsNullOrWhiteSpace(item.Title) ||
        item.Title.Length > 255 ||
        item.Quantity < 1 ||
        !IsWholeNonNegativeNoor(item.BasePriceInNoor) ||
        !IsWholeNonNegativeNoor(item.ItemDiscountInNoor) ||
        !IsWholeNonNegativeNoor(item.CouponDiscountInNoor) ||
        !IsWholeNonNegativeNoor(item.PriceInNoor) ||
        item.LineTotalInNoor - item.ItemDiscountInNoor - item.CouponDiscountInNoor != item.PriceInNoor;

    private static bool IsWholeNonNegativeNoor(decimal amount) =>
        amount >= 0 && decimal.Truncate(amount) == amount;

    public void AwaitTopUp(Guid id)
    {
        if (Status is not (CartStatus.Ready or CartStatus.AwaitingTopUp))
        {
            throw new InvalidOperationException();
        }
        TopUpRequestId = id;
        Status = CartStatus.AwaitingTopUp;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reopen()
    {
        if (Status is not (CartStatus.Failed or CartStatus.Expired) || !Synchronized)
        {
            throw new InvalidOperationException();
        }
        TopUpRequestId = null;
        ReservationId = Guid.NewGuid();
        _items.Clear();
        ItemDiscountInNoor = 0;
        CouponDiscountInNoor = 0;
        ShippingAmountInNoor = 0;
        ShippingRuleLabel = null;
        ShippingOriginId = null;
        ShippingAddress = null;
        Status = CartStatus.Preparing;
        Synchronized = false;
        LastSyncErrorCode = null;
        UpdatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30);
    }

    public void MarkPaid()
    {
        if (Status is not (CartStatus.Ready or CartStatus.AwaitingTopUp))
        {
            throw new InvalidOperationException();
        }
        Status = CartStatus.Paid;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Fail(bool expired = false)
    {
        if (Status == CartStatus.Paid)
        {
            return;
        }
        if (expired && (Status is not (CartStatus.Preparing or CartStatus.Ready) || ExpiresAtUtc > DateTime.UtcNow))
        {
            return;
        }
        Status = expired ? CartStatus.Expired : CartStatus.Failed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RecordSynchronization(bool succeeded, string? errorCode)
    {
        if (Synchronized)
        {
            return;
        }
        SyncAttempts++;
        LastSyncAttemptAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        Synchronized = succeeded;
        LastSyncErrorCode = succeeded ? null : errorCode;
    }
}
