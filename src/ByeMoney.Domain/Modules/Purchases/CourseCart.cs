namespace ByeMoney.Domain.Modules.Purchases;

public enum CourseCartStatus { Preparing, Ready, AwaitingTopUp, Paid, Failed, Expired }

public sealed record CourseCartItem(string ExternalCourseId, string Title, decimal BasePriceInNoor,
    decimal ItemDiscountInNoor, decimal CouponDiscountInNoor, decimal PriceInNoor);

public sealed class CourseCart
{
    public Guid Id { get; private set; }
    public Guid BuyerId { get; private set; }
    public string BuyerExternalUserId { get; private set; } = null!;
    public string[] ExternalCourseIds { get; private set; } = [];
    public string? CouponCode { get; private set; }
    private readonly List<CourseCartItem> _items = [];
    public IReadOnlyList<CourseCartItem> Items => _items.OrderBy(x => x.ExternalCourseId, StringComparer.Ordinal).ToArray();
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
    public decimal Total => Items.Sum(x => x.PriceInNoor);

    private CourseCart() { }
    public static CourseCart Create(Guid id, Guid buyerId, string externalUserId, string[] ids, string? couponCode) => new()
    {
        Id = id, ReservationId = id, BuyerId = buyerId, BuyerExternalUserId = externalUserId,
        ExternalCourseIds = ids.Order(StringComparer.Ordinal).ToArray(), CouponCode = couponCode?.Trim().ToUpperInvariant(),
        Status = CourseCartStatus.Preparing, CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
    };
    public bool Matches(string[] ids, string? couponCode) => ExternalCourseIds.SequenceEqual(ids.Order(StringComparer.Ordinal)) &&
        CouponCode == couponCode?.Trim().ToUpperInvariant();
    public void SetQuote(IReadOnlyList<CourseCartItem> items)
    {
        if (Status != CourseCartStatus.Preparing) return;
        if (items.Count != ExternalCourseIds.Length || items.Sum(x => x.BasePriceInNoor) > 99999999999999m || !items.Select(x => x.ExternalCourseId).SequenceEqual(ExternalCourseIds) ||
            items.Any(x => string.IsNullOrWhiteSpace(x.Title) || x.Title.Length > 255 || x.BasePriceInNoor < 0 ||
                x.ItemDiscountInNoor < 0 || x.CouponDiscountInNoor < 0 || x.PriceInNoor < 0 ||
                decimal.Truncate(x.BasePriceInNoor) != x.BasePriceInNoor || decimal.Truncate(x.ItemDiscountInNoor) != x.ItemDiscountInNoor ||
                decimal.Truncate(x.CouponDiscountInNoor) != x.CouponDiscountInNoor || decimal.Truncate(x.PriceInNoor) != x.PriceInNoor ||
                x.BasePriceInNoor - x.ItemDiscountInNoor - x.CouponDiscountInNoor != x.PriceInNoor))
            throw new ArgumentException(nameof(items));
        _items.Clear();
        _items.AddRange(items);
        Status = CourseCartStatus.Ready;
        UpdatedAtUtc = DateTime.UtcNow;
    }
    public void AwaitTopUp(Guid id)
    {
        if (Status != CourseCartStatus.Ready) throw new InvalidOperationException();
        TopUpRequestId = id; TopUpRequestIds = TopUpRequestIds.Append(id).Distinct().ToArray();
        Status = CourseCartStatus.AwaitingTopUp; UpdatedAtUtc = DateTime.UtcNow;
    }
    public void Reopen()
    {
        if (Status is not (CourseCartStatus.Failed or CourseCartStatus.Expired) || !Synchronized)
            throw new InvalidOperationException();
        if (TopUpRequestId is Guid previous) TopUpRequestIds = TopUpRequestIds.Append(previous).Distinct().ToArray();
        TopUpRequestId = null;
        ReservationId = Guid.NewGuid();
        _items.Clear();
        Status = CourseCartStatus.Preparing;
        Synchronized = false;
        LastSyncErrorCode = null;
        UpdatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30);
    }
    public void MarkPaid()
    {
        if (Status is not (CourseCartStatus.Ready or CourseCartStatus.AwaitingTopUp)) throw new InvalidOperationException();
        Status = CourseCartStatus.Paid; UpdatedAtUtc = DateTime.UtcNow;
    }
    public void Fail(bool expired = false)
    {
        if (Status == CourseCartStatus.Paid) return;
        if (expired && (Status is not (CourseCartStatus.Preparing or CourseCartStatus.Ready) || ExpiresAtUtc > DateTime.UtcNow)) return;
        Status = expired ? CourseCartStatus.Expired : CourseCartStatus.Failed; UpdatedAtUtc = DateTime.UtcNow;
    }
    public void RecordSynchronization(bool succeeded, string? errorCode)
    {
        if (Synchronized) return;
        SyncAttempts++; LastSyncAttemptAtUtc = DateTime.UtcNow; UpdatedAtUtc = DateTime.UtcNow;
        Synchronized = succeeded; LastSyncErrorCode = succeeded ? null : errorCode;
    }
}
