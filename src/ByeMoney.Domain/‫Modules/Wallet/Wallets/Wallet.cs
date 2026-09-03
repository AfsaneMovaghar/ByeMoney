using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;

namespace ByeMoney.Domain.Modules.Wallet.Wallets;

public class Wallet : BaseEntity<WalletId>
{
    public AccountId AccountId { get; private set; }
    public UserId UserId { get; private set; }
    public decimal Balance { get; private set; }
    public DateTime LastUpdatedAtUtc { get; private set; }

    private Wallet() { }

    public static Wallet Create(AccountId accountId, UserId userId)
    {
        return new Wallet
        {
            Id = WalletId.New(),
            AccountId = accountId,
            UserId = userId,
            Balance = 0m,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAtUtc = DateTime.UtcNow
        };
    }

    public void ApplyCredit(decimal amount)
    {
        if (amount <= 0)
            throw new DomainException("Credit amount must be greater than zero.");

        Balance += amount;
        LastUpdatedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyDebit(decimal amount)
    {
        if (amount <= 0)
            throw new DomainException("Debit amount must be greater than zero.");

        if (Balance < amount)
            throw new DomainException("Insufficient wallet balance.");

        Balance -= amount;
        LastUpdatedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}

