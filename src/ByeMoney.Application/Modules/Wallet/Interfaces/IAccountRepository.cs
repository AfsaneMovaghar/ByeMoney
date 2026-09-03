using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface IAccountRepository : IRepository<Account, AccountId>
{
    Task<Account?> GetByUserIdAsync(UserId userId, CancellationToken ct = default);
    Task<Account?> GetSystemAccountAsync(CancellationToken ct = default);
}

