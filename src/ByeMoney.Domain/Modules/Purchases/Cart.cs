namespace ByeMoney.Domain.Modules.Purchases;

public sealed class Cart
{
    public Guid Id { get; private set; }
    public Guid BuyerId { get; private set; }
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
        .OrderBy(x => x.ExternalCourseId, StringComparer.Ordinal)
        .ThenBy(x => x.ItemType)
        .ToArray();

    public decimal ItemDiscountInNoor { get; private set; }
    public decimal CouponDiscountInNoor { get; private set; }
    public decimal ShippingAmountInNoor { get; private set; }
    public string? ShippingRuleLabel { get; private set; }
    public CartStatus Status { get; private set; }
    public Guid? TopUpRequestId { get; private set; }
    public Guid[] TopUpRequestIds { get; private set; } = [];
    public Guid ReservationId { get; private set; }
    public bool Synchronized { get; private set; }
    public int SyncAttempts { get; private set; }
    public DateTime? LastSyncAttemptAtUtc { get; private set; }
    public string? LastSyncErrorCode { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public decimal Total => Items.Sum(x => x.LineTotalInNoor) - ItemDiscountInNoor - CouponDiscountInNoor + ShippingAmountInNoor;
    public decimal OriginalTotal => Items.Sum(x => x.LineTotalInNoor);

    private const decimal MaximumOriginalTotalInNoor = 99999999999999m;

    private Cart()
    {
    }

    public static Cart Create(Guid id, Guid buyerId, string externalUserId, CartItemInput[] items, string? couponCode) => new()
    {
        Id = id,
        ReservationId = id,
        BuyerId = buyerId,
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
        string? shippingRuleLabel = null)
    {
        if (Status != CartStatus.Preparing)
        {
            return;
        }

        if (items.Count != RequestedItemSignatures.Length ||
            items.Sum(x => x.LineTotalInNoor) > MaximumOriginalTotalInNoor ||
            !IsWholeNonNegativeNoor(shippingAmountInNoor) ||
            items.Any(HasInvalidPrice) ||
            items.GroupBy(x => (x.ExternalCourseId, x.Type)).Any(g => g.Count() > 1) ||
            items.Any(x => !Enum.IsDefined(x.Type) ||
                (x.Type == ProductItemType.CourseChapter
                    ? string.IsNullOrWhiteSpace(x.ParentExternalId) || x.ParentExternalId.Length > 100 ||
                      x.ParentExternalId == x.ExternalCourseId || x.Quantity != 1
                    : x.Type == ProductItemType.Course
                        ? x.ParentExternalId is not null || x.Quantity != 1
                        : x.ParentExternalId is not null || x.Quantity < 1)))
        {
            throw new ArgumentException(nameof(items));
        }

        var signatures = items
            .Select(x => CartItemSignature.Format(x.ExternalCourseId, x.Type, x.Quantity))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!signatures.SequenceEqual(RequestedItemSignatures))
        {
            throw new ArgumentException(nameof(items));
        }

        _items.Clear();
        _items.AddRange(items.Select(x => new CartItem(
            x.ExternalCourseId, x.Title, x.BasePriceInNoor, x.PriceInNoor, x.Type, x.ParentExternalId, x.Quantity)));
        ItemDiscountInNoor = items.Sum(x => x.ItemDiscountInNoor);
        CouponDiscountInNoor = items.Sum(x => x.CouponDiscountInNoor);
        ShippingAmountInNoor = shippingAmountInNoor;
        ShippingRuleLabel = string.IsNullOrWhiteSpace(shippingRuleLabel) ? null : shippingRuleLabel.Trim();

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
        TopUpRequestIds = TopUpRequestIds.Append(id).Distinct().ToArray();
        Status = CartStatus.AwaitingTopUp;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reopen()
    {
        if (Status is not (CartStatus.Failed or CartStatus.Expired) || !Synchronized)
        {
            throw new InvalidOperationException();
        }
        if (TopUpRequestId is Guid previous)
        {
            TopUpRequestIds = TopUpRequestIds.Append(previous).Distinct().ToArray();
        }
        TopUpRequestId = null;
        ReservationId = Guid.NewGuid();
        _items.Clear();
        ItemDiscountInNoor = 0;
        CouponDiscountInNoor = 0;
        ShippingAmountInNoor = 0;
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
