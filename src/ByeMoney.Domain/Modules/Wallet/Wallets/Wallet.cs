using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Resources;

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
            LastUpdatedAtUtc = DateTime.UtcNow
        };
    }

    public void ApplyCredit(decimal amount)
    {
        if (amount <= 0)
            throw new DomainException(DomainErrors.Wallet_CreditMustBeGreaterThanZero);

        Balance += amount;
        LastUpdatedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyDebit(decimal amount)
    {
        if (amount <= 0)
            throw new DomainException(DomainErrors.Wallet_DebitMustBeGreaterThanZero);

        if (Balance < amount)
            throw new DomainException(DomainErrors.Wallet_InsufficientBalance);

        Balance -= amount;
        LastUpdatedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}

