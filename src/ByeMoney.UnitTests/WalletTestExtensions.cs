using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;

namespace ByeMoney.UnitTests;

public static class WalletTestExtensions
{
    public static void ApplyCredit(this Wallet wallet, decimal amount)
    {
        wallet.Credit(amount, LedgerBalanceType.Regular);
    }

    public static void ApplyDebit(this Wallet wallet, decimal amount)
    {
        wallet.Debit(amount, WalletDebitPurpose.WithdrawalOrTransfer);
    }

    public static void ApplyGiftCredit(this Wallet wallet, decimal amount)
    {
        wallet.Credit(amount, LedgerBalanceType.Gift);
    }
}

