using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface IWalletRepository : IRepository<WalletEntity, WalletId>
{
    Task<WalletEntity?> GetByUserIdAsync(UserId userId, CancellationToken ct = default);
    Task<WalletEntity?> GetByAccountIdAsync(AccountId accountId, CancellationToken ct = default);
}

