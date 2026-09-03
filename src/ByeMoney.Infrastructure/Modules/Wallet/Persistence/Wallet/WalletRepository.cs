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
}

