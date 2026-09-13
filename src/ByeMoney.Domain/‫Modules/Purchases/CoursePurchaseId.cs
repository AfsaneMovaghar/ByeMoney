namespace ByeMoney.Domain.Modules.Purchases;

public readonly record struct CoursePurchaseId(Guid Value)
{
    public static CoursePurchaseId New() => new(Guid.NewGuid());
    public static CoursePurchaseId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
