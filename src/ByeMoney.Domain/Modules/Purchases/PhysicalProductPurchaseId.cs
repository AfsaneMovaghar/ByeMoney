namespace ByeMoney.Domain.Modules.Purchases;

public readonly record struct PhysicalProductPurchaseId(Guid Value)
{
    public static PhysicalProductPurchaseId New() => new(Guid.NewGuid());
    public static PhysicalProductPurchaseId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
