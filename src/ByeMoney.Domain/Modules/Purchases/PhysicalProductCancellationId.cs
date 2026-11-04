namespace ByeMoney.Domain.Modules.Purchases;

public readonly record struct PhysicalProductCancellationId(Guid Value)
{
    public static PhysicalProductCancellationId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
