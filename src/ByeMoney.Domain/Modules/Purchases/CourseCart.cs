namespace ByeMoney.Domain.Modules.Purchases;

public sealed class CourseCart
{
    public Guid Id { get; private set; }
    public Guid BuyerId { get; private set; }
    public string BuyerExternalUserId { get; private set; } = null!;
    public string[] ExternalCourseIds { get; private set; } = [];
    public string? CouponCode { get; private set; }
    private readonly List<CourseCartItem> _items = [];
    public IReadOnlyList<CourseCartItem> Items => _items.OrderBy(x => x.ExternalCourseId, StringComparer.Ordinal).ToArray();
    private readonly List<CourseCartPurchaseAllocation> _purchaseAllocations = [];
    public IReadOnlyList<CourseCartPurchaseAllocation> PurchaseAllocations =>
        _purchaseAllocations.OrderBy(x => x.ExternalCourseId, StringComparer.Ordinal).ToArray();
    public decimal ItemDiscountInNoor { get; private set; }
    public decimal CouponDiscountInNoor { get; private set; }
    public decimal ShippingAmountInNoor { get; private set; }
    public string? ShippingRuleLabel { get; private set; }
    public CourseCartStatus Status { get; private set; }
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

    private CourseCart()
    {
    }

    public static string FormatSignature(CartItemInput item)
    {
        if (item.Type is ProductItemType.Course or ProductItemType.CourseChapter && item.Quantity == 1)
        {
            return item.ExternalId;
        }
        return $"{item.Type}:{item.ExternalId}:{item.Quantity}";
    }

    public static string FormatSignature(CourseCartQuoteItem item)
    {
        if (item.Type is ProductItemType.Course or ProductItemType.CourseChapter && item.Quantity == 1)
        {
            return item.ExternalCourseId;
        }
        return $"{item.Type}:{item.ExternalCourseId}:{item.Quantity}";
    }

    public static CourseCart Create(Guid id, Guid buyerId, string externalUserId, CartItemInput[] items, string? couponCode) => new()
    {
        Id = id,
        ReservationId = id,
        BuyerId = buyerId,
        BuyerExternalUserId = externalUserId,
        ExternalCourseIds = items.Select(FormatSignature).Order(StringComparer.Ordinal).ToArray(),
        CouponCode = couponCode?.Trim().ToUpperInvariant(),
        Status = CourseCartStatus.Preparing,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
    };

    public static CourseCart Create(Guid id, Guid buyerId, string externalUserId, string[] ids, string? couponCode) =>
        Create(id, buyerId, externalUserId, ids.Select(x => new CartItemInput(x, ProductItemType.Course, 1)).ToArray(), couponCode);

    public bool Matches(CartItemInput[] items, string? couponCode) =>
        ExternalCourseIds.SequenceEqual(items.Select(FormatSignature).Order(StringComparer.Ordinal)) &&
        CouponCode == couponCode?.Trim().ToUpperInvariant();

    public bool Matches(string[] ids, string? couponCode) =>
        ExternalCourseIds.SequenceEqual(ids.Order(StringComparer.Ordinal)) &&
        CouponCode == couponCode?.Trim().ToUpperInvariant();

    public void SetQuote(
        IReadOnlyList<CourseCartQuoteItem> items,
        decimal shippingAmountInNoor = 0m,
        string? shippingRuleLabel = null)
    {
        if (Status != CourseCartStatus.Preparing)
        {
            return;
        }

        if (items.Count != ExternalCourseIds.Length ||
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

        var signatures = items.Select(FormatSignature).Order(StringComparer.Ordinal).ToArray();
        if (!signatures.SequenceEqual(ExternalCourseIds))
        {
            throw new ArgumentException(nameof(items));
        }

        _items.Clear();
        _items.AddRange(items.Select(x => new CourseCartItem(
            x.ExternalCourseId, x.Title, x.BasePriceInNoor, x.Type, x.ParentExternalId, x.Quantity)));
        _purchaseAllocations.Clear();
        _purchaseAllocations.AddRange(items.Select(x => new CourseCartPurchaseAllocation(x.ExternalCourseId, x.PriceInNoor, x.Type)));
        ItemDiscountInNoor = items.Sum(x => x.ItemDiscountInNoor);
        CouponDiscountInNoor = items.Sum(x => x.CouponDiscountInNoor);
        ShippingAmountInNoor = shippingAmountInNoor;
        ShippingRuleLabel = string.IsNullOrWhiteSpace(shippingRuleLabel) ? null : shippingRuleLabel.Trim();

        if (_purchaseAllocations.Sum(x => x.AmountInNoor) + ShippingAmountInNoor != Total)
        {
            throw new ArgumentException("Allocations plus shipping must match cart total.");
        }

        Status = CourseCartStatus.Ready;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static bool HasInvalidPrice(CourseCartQuoteItem item) =>
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
        if (Status != CourseCartStatus.Ready)
        {
            throw new InvalidOperationException();
        }
        TopUpRequestId = id;
        TopUpRequestIds = TopUpRequestIds.Append(id).Distinct().ToArray();
        Status = CourseCartStatus.AwaitingTopUp;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reopen()
    {
        if (Status is not (CourseCartStatus.Failed or CourseCartStatus.Expired) || !Synchronized)
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
        _purchaseAllocations.Clear();
        ItemDiscountInNoor = 0;
        CouponDiscountInNoor = 0;
        ShippingAmountInNoor = 0;
        Status = CourseCartStatus.Preparing;
        Synchronized = false;
        LastSyncErrorCode = null;
        UpdatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30);
    }

    public void MarkPaid()
    {
        if (Status is not (CourseCartStatus.Ready or CourseCartStatus.AwaitingTopUp))
        {
            throw new InvalidOperationException();
        }
        Status = CourseCartStatus.Paid;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Fail(bool expired = false)
    {
        if (Status == CourseCartStatus.Paid)
        {
            return;
        }
        if (expired && (Status is not (CourseCartStatus.Preparing or CourseCartStatus.Ready) || ExpiresAtUtc > DateTime.UtcNow))
        {
            return;
        }
        Status = expired ? CourseCartStatus.Expired : CourseCartStatus.Failed;
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

