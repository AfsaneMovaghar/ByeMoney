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

    public async Task<WalletEntity?> GetByAccountIdAsync(AccountId accountId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(w => w.AccountId == accountId, ct);
    }

    public async Task<WalletEntity?> GetByUserIdAsync(UserId userId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(w => w.UserId == userId, ct);
    }

    /// <summary>
    /// ضرورت SQL خام و هزینه جایگزین (مطابق بند ۱ راهنمای کلین‌کد و دیتابیس):
    /// برای عملیات کسر مالی و جلوگیری از Double Spending و Race Condition در محیط‌های توزیع‌شده یا همزمان،
    /// نیاز به قفل ردیفی بدبینانه (SELECT ... FOR UPDATE) است تا سهم‌بندی مانده هدیه و عادی روی مانده تازه و قطعی انجام گیرد.
    /// به دلیل عدم وجود متد معادل در EF Core LINQ برای FOR UPDATE، کوئری پارامتری محصور در Infrastructure پیاده‌سازی شده است.
    /// </summary>
    public async Task<WalletEntity?> GetByUserIdForUpdateAsync(UserId userId, CancellationToken ct = default)
    {
        if (Context.Database.IsNpgsql())
        {
            var rawGuid = userId.Value;
            return await DbSet
                .FromSqlInterpolated($"SELECT * FROM \"Wallets\" WHERE \"UserId\" = {rawGuid} FOR UPDATE")
                .FirstOrDefaultAsync(ct);
        }

        return await GetByUserIdAsync(userId, ct);
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
            select new { user.ExternalUserId, Total = wallet.RegularBalance + wallet.GiftBalance })
            .AsNoTracking()
            .ToListAsync(ct);

        return balances.ToDictionary(x => x.ExternalUserId, x => x.Total, StringComparer.Ordinal);
    }

    public async Task<(Dictionary<string, decimal> Balances, Dictionary<string, decimal> GiftBalances)> GetBalancesAndGiftBalancesByExternalUserIdsAsync(
        IReadOnlyCollection<string> externalUserIds, CancellationToken ct = default)
    {
        if (externalUserIds.Count == 0)
        {
            return (new Dictionary<string, decimal>(StringComparer.Ordinal), new Dictionary<string, decimal>(StringComparer.Ordinal));
        }

        var results = await (
            from user in Context.Set<User>()
            join wallet in DbSet on user.Id equals wallet.UserId
            where externalUserIds.Contains(user.ExternalUserId)
            select new { user.ExternalUserId, wallet.RegularBalance, wallet.GiftBalance })
            .AsNoTracking()
            .ToListAsync(ct);

        var balances = results.ToDictionary(x => x.ExternalUserId, x => x.RegularBalance + x.GiftBalance, StringComparer.Ordinal);
        var giftBalances = results.ToDictionary(x => x.ExternalUserId, x => x.GiftBalance, StringComparer.Ordinal);

        return (balances, giftBalances);
    }
}

