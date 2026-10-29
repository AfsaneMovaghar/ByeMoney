using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface IWalletPostingService
{
    Task<WalletDebitResult> PostDebitAsync(
        ProvisionedUserWallet userWallet,
        Account systemAccount,
        decimal amount,
        WalletDebitPurpose purpose,
        Guid transactionId,
        LedgerReferenceType referenceType,
        string? referenceId,
        UserId? performedByUserId,
        CancellationToken ct);

    Task<WalletCreditResult> PostCreditAsync(
        ProvisionedUserWallet userWallet,
        Account systemAccount,
        decimal amount,
        LedgerBalanceType balanceType,
        Guid transactionId,
        LedgerReferenceType referenceType,
        string? referenceId,
        UserId? performedByUserId,
        CancellationToken ct);
}

