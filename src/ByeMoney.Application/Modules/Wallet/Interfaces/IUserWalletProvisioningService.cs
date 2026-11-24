using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface IUserWalletProvisioningService
{
    /// <summary>
    /// Gets or lazily creates both UserAccount and Wallet in a consistent, atomic manner.
    /// </summary>
    Task<ProvisionedUserWallet> GetOrCreateUserWalletAsync(UserId userId, string scopeId, CancellationToken ct = default);

    /// <summary>
    /// حساب سیستم از پیش ساخته را برای همین محدوده برمی‌گرداند و هرگز حساب تازه نمی‌سازد.
    /// </summary>
    Task<Account> GetOrCreateSystemAccountAsync(string scopeId, CancellationToken ct = default);
}
