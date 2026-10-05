namespace ByeMoney.Domain.Modules.Wallet.Wallets;

public readonly record struct WalletId(Guid Value)
{
    public static WalletId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

