namespace ByeMoney.Domain.Modules.Purchases;

/// <summary>
/// سفارشی که در یک دورهٔ تسویه ثبت شده است. شناسهٔ سفارش کلید اصلی است،
/// پس هر سفارش حداکثر در یک دوره می‌آید، حتی اگر دو ادمین هم‌زمان دوره را ببندند.
/// مقادیر Snapshot لحظهٔ بستن‌اند و بعداً تغییر نمی‌کنند.
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
    public decimal NetAmountInNoor { get; private set; }

    private OrderSettlementItem() { }

    public static OrderSettlementItem Create(
        string orderId,
        string sourceType,
        string buyerExternalUserId,
        DateTime orderCreatedAtUtc,
        decimal netAmountInNoor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(buyerExternalUserId);
        ArgumentOutOfRangeException.ThrowIfNegative(netAmountInNoor);

        return new OrderSettlementItem
        {
            OrderId = orderId,
            SourceType = sourceType,
            BuyerExternalUserId = buyerExternalUserId,
            OrderCreatedAtUtc = orderCreatedAtUtc,
            NetAmountInNoor = netAmountInNoor
        };
    }

    internal void AttachTo(OrderSettlementPeriodId periodId)
    {
        PeriodId = periodId;
    }
}
