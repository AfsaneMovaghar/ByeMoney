using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Resources;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.Application.Modules.Wallet.Services;

/// <summary>
/// هر جفت‌ثبت دفتر کل فقط بین دو حساب هم‌محدوده مجاز است. دفتر کل خودش ستون محدوده ندارد.
/// </summary>
public static class LedgerScopeGuard
{
    public static void EnsureSameScope(Account userAccount, Account systemAccount, WalletEntity? wallet = null)
    {
        ArgumentNullException.ThrowIfNull(userAccount);
        ArgumentNullException.ThrowIfNull(systemAccount);

        if (userAccount.ScopeId != systemAccount.ScopeId ||
            wallet is not null && wallet.ScopeId != userAccount.ScopeId)
        {
            throw new DomainException(DomainErrors.Ledger_AccountsMustShareScope);
        }
    }
}
