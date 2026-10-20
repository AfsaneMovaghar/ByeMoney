using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface IUserWalletProvisioningService
{
    /// <summary>
    /// Gets or lazily creates both UserAccount and Wallet in a consistent, atomic manner.
    /// </summary>
    Task<ProvisionedUserWallet> GetOrCreateUserWalletAsync(UserId userId, CancellationToken ct = default);

    /// <summary>
    /// Gets or lazily creates the singleton System Account.
    /// </summary>
    Task<Account> GetOrCreateSystemAccountAsync(CancellationToken ct = default);
}
