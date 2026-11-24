using ByeMoney.Application.Modules.Wallet.Transactions.DTOs;
using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Application.Modules.Wallet.Transactions.Interfaces;

public interface IWalletTransactionReader
{
    /// <summary>
    /// گردش حساب کیف پول همان کاربر، جدیدترین اول. کاربر بدون کیف پول صفحهٔ خالی می‌گیرد.
    /// </summary>
    Task<WalletTransactionPage<WalletTransactionRow>> ListAsync(
        UserId userId, string scopeId, int page, int pageSize, CancellationToken ct);
}
