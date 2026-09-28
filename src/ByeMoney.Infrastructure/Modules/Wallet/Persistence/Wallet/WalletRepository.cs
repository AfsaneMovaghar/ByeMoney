using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByeMoney.Infrastructure.Modules.Wallet.Persistence.Wallet;

using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

public class WalletRepository : BaseRepository<WalletEntity, WalletId>, IWalletRepository
{
    public WalletRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<WalletEntity?> GetByUserIdAsync(UserId userId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(w => w.UserId.Equals(userId), ct);
    }

    public async Task<WalletEntity?> GetByAccountIdAsync(AccountId accountId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(w => w.AccountId.Equals(accountId), ct);
    }

    public async Task<Dictionary<string, decimal>> GetBalancesByExternalUserIdsAsync(
        IReadOnlyCollection<string> externalUserIds, CancellationToken ct = default)
    {
        if (externalUserIds.Count == 0)
        {
            return new Dictionary<string, decimal>(StringComparer.Ordinal);
        }

        var balances = await (
            from user in Context.Set<User>()
            join wallet in DbSet on user.Id equals wallet.UserId
            where externalUserIds.Contains(user.ExternalUserId)
            select new { user.ExternalUserId, wallet.Balance })
            .AsNoTracking()
            .ToListAsync(ct);

        return balances.ToDictionary(x => x.ExternalUserId, x => x.Balance, StringComparer.Ordinal);
    }
}

