using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet;

namespace ByeMoney.Domain.Modules.Wallet.Accounts;

public class Account : BaseEntity<AccountId>
{
    public static readonly AccountId SystemAccountId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    public AccountType Type { get; private set; }
    public UserId? UserId { get; private set; }
    public string ScopeId { get; private set; } = null!;
    public AccountStatus Status { get; private set; }

    private Account() { }

    public static Account CreateUserAccount(UserId userId, string scopeId)
    {
        return new Account
        {
            Id = AccountId.New(),
            Type = AccountType.User,
            UserId = userId,
            ScopeId = FinancialScope.Require(scopeId),
            Status = AccountStatus.Active
        };
    }

    public static Account CreateSystemAccount(string scopeId, AccountId? id = null)
    {
        return new Account
        {
            Id = id ?? SystemAccountId,
            Type = AccountType.System,
            UserId = null,
            ScopeId = FinancialScope.Require(scopeId),
            Status = AccountStatus.Active
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

