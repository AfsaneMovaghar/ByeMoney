namespace ByeMoney.Domain.Modules.Wallet.Withdrawals;

public readonly record struct NoorWithdrawalId(Guid Value)
{
    public static NoorWithdrawalId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
