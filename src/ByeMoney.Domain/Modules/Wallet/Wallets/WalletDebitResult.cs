namespace ByeMoney.Domain.Modules.Wallet.Wallets;

public sealed record WalletDebitResult(decimal GiftDebited, decimal RegularDebited)
{
    public decimal TotalDebited => checked(GiftDebited + RegularDebited);
}

