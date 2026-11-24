using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet;
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

    public async Task<ProvisionedUserWallet> GetOrCreateUserWalletAsync(UserId userId, string scopeId, CancellationToken ct = default)
    {
        scopeId = FinancialScope.Require(scopeId);
        var account = await _accountRepository.GetByUserIdAsync(userId, scopeId, ct);
        if (account is null)
        {
            account = Account.CreateUserAccount(userId, scopeId);
            await _accountRepository.AddAsync(account, ct);
        }

        var wallet = await _walletRepository.GetByAccountIdAsync(account.Id, ct);
        if (wallet is null)
        {
            wallet = WalletEntity.Create(account.Id, userId, scopeId);
            await _walletRepository.AddAsync(wallet, ct);
        }

        return new ProvisionedUserWallet(account, wallet);
    }

    public async Task<Account> GetOrCreateSystemAccountAsync(string scopeId, CancellationToken ct = default)
    {
        scopeId = FinancialScope.Require(scopeId);
        return await _accountRepository.GetSystemAccountAsync(scopeId, ct)
            ?? throw new InvalidOperationException("System account has not been provisioned.");
    }
}
