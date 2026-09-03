namespace ByeMoney.Domain.Modules.Wallet.TopUps;

public readonly record struct TopUpRequestId(Guid Value)
{
    public static TopUpRequestId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

