namespace ByeMoney.Domain.Modules.Purchases;

/// <summary>
/// سفارشی که در یک دورهٔ تسویه ثبت شده است. شناسهٔ سفارش کلید اصلی است،
/// پس هر سفارش حداکثر در یک دوره می‌آید، حتی اگر دو ادمین هم‌زمان دوره را ببندند.
/// مقادیر Snapshot لحظهٔ بستن‌اند و بعداً تغییر نمی‌کنند. نور خالص به سهم عادی و هدیه تفکیک می‌شود،
/// چون نور هدیه پشتوانهٔ ریالی ندارد و ادمین مبنای انتقال دستی را خودش انتخاب می‌کند.
/// </summary>
public class OrderSettlementItem
{
    public const int OrderIdMaxLength = 64;
    public const int SourceTypeMaxLength = 20;
    public const int BuyerExternalUserIdMaxLength = 100;

    public string OrderId { get; private set; } = null!;
    public OrderSettlementPeriodId PeriodId { get; private set; }
    public string SourceType { get; private set; } = null!;
    public string BuyerExternalUserId { get; private set; } = null!;
    public DateTime OrderCreatedAtUtc { get; private set; }
    public decimal RegularNetAmountInNoor { get; private set; }
    public decimal GiftNetAmountInNoor { get; private set; }

    /// <summary>جمع کل نور خالص؛ همیشه برابر سهم عادی به‌علاوهٔ سهم هدیه.</summary>
    public decimal NetAmountInNoor { get; private set; }

    private OrderSettlementItem() { }

    public static OrderSettlementItem Create(
        string orderId,
        string sourceType,
        string buyerExternalUserId,
        DateTime orderCreatedAtUtc,
        PaymentShares netShares)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(buyerExternalUserId);
        ArgumentOutOfRangeException.ThrowIfNegative(netShares.RegularInNoor);
        ArgumentOutOfRangeException.ThrowIfNegative(netShares.GiftInNoor);

        return new OrderSettlementItem
        {
            OrderId = orderId,
            SourceType = sourceType,
            BuyerExternalUserId = buyerExternalUserId,
            OrderCreatedAtUtc = orderCreatedAtUtc,
            RegularNetAmountInNoor = netShares.RegularInNoor,
            GiftNetAmountInNoor = netShares.GiftInNoor,
            NetAmountInNoor = netShares.RegularInNoor + netShares.GiftInNoor
        };
    }

    internal void AttachTo(OrderSettlementPeriodId periodId)
    {
        PeriodId = periodId;
    }
}
