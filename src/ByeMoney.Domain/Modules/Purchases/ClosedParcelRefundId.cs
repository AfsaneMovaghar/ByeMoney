namespace ByeMoney.Domain.Modules.Purchases;

public readonly record struct ClosedParcelRefundId(Guid Value)
{
    public static ClosedParcelRefundId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
