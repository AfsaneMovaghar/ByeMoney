using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public record ProvisionedUserWallet(Account Account, WalletEntity Wallet);

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
