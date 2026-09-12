using ByeMoney.Domain._Modules.Wallet.Ledgers;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Wallet.Accounts;

namespace ByeMoney.Domain.Modules.Wallet.Ledgers;

public class LedgerEntry : BaseEntity<LedgerEntryId>
{
    public AccountId AccountId { get; private set; }
    public decimal Amount { get; private set; }
    public Guid TransactionId { get; private set; }
    public string? ReferenceId { get; private set; }
    public LedgerReferenceType ReferenceType { get; private set; }

    private LedgerEntry() { }

    public static LedgerEntry Create(
        AccountId accountId,
        decimal amount,
        Guid transactionId,
        LedgerReferenceType refrenceType,
        string? referenceId = null)
    {
        return new LedgerEntry
        {
            Id = LedgerEntryId.New(),
            AccountId = accountId,
            Amount = amount,
            TransactionId = transactionId,
            ReferenceId = referenceId,
            ReferenceType = refrenceType
        };
    }
}

