using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence.Account;

using AccountEntity = ByeMoney.Domain.Modules.Wallet.Accounts.Account;

public class AccountRepository : BaseRepository<AccountEntity, AccountId>, IAccountRepository
{
    public AccountRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<AccountEntity?> GetByUserIdAsync(UserId userId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(a => a.UserId.HasValue && a.UserId.Value.Equals(userId), ct);
    }

    public async Task<AccountEntity?> GetSystemAccountAsync(CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(a => a.Type == AccountType.System, ct);
    }
}

