namespace ByeMoney.Domain.Modules.Wallet.Wallets;

public sealed record WalletCreditResult(decimal GiftCredited, decimal RegularCredited)
{
    public decimal TotalCredited => checked(GiftCredited + RegularCredited);
}

