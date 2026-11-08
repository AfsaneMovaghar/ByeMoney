namespace ByeMoney.Domain.Modules.Wallet.Ledgers;

public enum LedgerReferenceType
{
    TopUp = 1,
    Purchase = 2,
    Refund = 3,
    AdminGrant = 4,
    WelcomeGift = 5,

    /// <summary>خروج نور عادی توسط ادمین؛ مرجع، شناسهٔ رکورد خروج است (D4).</summary>
    Withdrawal = 6,

    /// <summary>جبران خروجی که واریزش انجام نشد؛ مرجع، همان رکورد خروج است.</summary>
    WithdrawalReversal = 7
}

