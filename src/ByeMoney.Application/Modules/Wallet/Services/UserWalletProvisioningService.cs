using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.Application.Modules.Wallet.Services;

public class UserWalletProvisioningService : IUserWalletProvisioningService
{
    private readonly IAccountRepository _accountRepository;
    private readonly IWalletRepository _walletRepository;

    public UserWalletProvisioningService(
        IAccountRepository accountRepository,
        IWalletRepository walletRepository)
    {
        _accountRepository = accountRepository;
        _walletRepository = walletRepository;
    }

    public async Task<ProvisionedUserWallet> GetOrCreateUserWalletAsync(UserId userId, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByUserIdAsync(userId, ct);
        if (account is null)
        {
            account = Account.CreateUserAccount(userId);
            await _accountRepository.AddAsync(account, ct);
        }

        var wallet = await _walletRepository.GetByAccountIdAsync(account.Id, ct);
        if (wallet is null)
        {
            wallet = WalletEntity.Create(account.Id, userId);
            await _walletRepository.AddAsync(wallet, ct);
        }

        return new ProvisionedUserWallet(account, wallet);
    }

    public async Task<Account> GetOrCreateSystemAccountAsync(CancellationToken ct = default)
    {
        var systemAccount = await _accountRepository.GetSystemAccountAsync(ct);
        if (systemAccount is null)
        {
            systemAccount = Account.CreateSystemAccount();
            await _accountRepository.AddAsync(systemAccount, ct);
        }

        return systemAccount;
    }
}
