namespace ByeMoney.Domain.Modules.Purchases;

public readonly record struct OrderSettlementPeriodId(Guid Value)
{
    public static OrderSettlementPeriodId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
