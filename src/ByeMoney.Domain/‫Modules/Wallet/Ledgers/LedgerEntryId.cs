namespace ByeMoney.Domain.Modules.Wallet.Ledgers;

public readonly record struct LedgerEntryId(Guid Value)
{
    public static LedgerEntryId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

