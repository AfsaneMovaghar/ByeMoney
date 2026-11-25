namespace ByeMoney.Domain.Modules.Purchases;

public readonly record struct PhysicalReservationCancellationId(Guid Value)
{
    public static PhysicalReservationCancellationId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
