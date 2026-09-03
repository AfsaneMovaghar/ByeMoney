using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Domain.Modules.Wallet.Accounts;

public class Account : BaseEntity<AccountId>
{
    public static readonly AccountId SystemAccountId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    public AccountType Type { get; private set; }
    public UserId? UserId { get; private set; }
    public AccountStatus Status { get; private set; }

    private Account() { }

    public static Account CreateUserAccount(UserId userId)
    {
        return new Account
        {
            Id = AccountId.New(),
            Type = AccountType.User,
            UserId = userId,
            Status = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Account CreateSystemAccount(AccountId? id = null)
    {
        return new Account
        {
            Id = id ?? SystemAccountId,
            Type = AccountType.System,
            UserId = null,
            Status = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Suspend()
    {
        Status = AccountStatus.Suspended;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        Status = AccountStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }
}

