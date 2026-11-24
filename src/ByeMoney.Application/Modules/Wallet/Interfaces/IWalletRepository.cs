using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface IWalletRepository : IRepository<WalletEntity, WalletId>
{
    Task<WalletEntity?> GetByUserIdAsync(UserId userId, string scopeId, CancellationToken ct = default);
    Task<WalletEntity?> GetByUserIdForUpdateAsync(UserId userId, string scopeId, CancellationToken ct = default);
    Task<WalletEntity?> GetByAccountIdAsync(AccountId accountId, CancellationToken ct = default);
    Task<Dictionary<string, decimal>> GetBalancesByExternalUserIdsAsync(IReadOnlyCollection<string> externalUserIds, string scopeId, CancellationToken ct = default);
    Task<(Dictionary<string, decimal> Balances, Dictionary<string, decimal> GiftBalances)> GetBalancesAndGiftBalancesByExternalUserIdsAsync(IReadOnlyCollection<string> externalUserIds, string scopeId, CancellationToken ct = default);
}

