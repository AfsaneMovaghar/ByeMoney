using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;

namespace ByeMoney.Application.Modules.Wallet.Services;

public sealed class WalletPostingService(
    IRepository<LedgerEntry, LedgerEntryId> ledgerRepository,
    IWalletRepository walletRepository) : IWalletPostingService
{
    public async Task<WalletDebitResult> PostDebitAsync(
        ProvisionedUserWallet userWallet,
        Account systemAccount,
        decimal amount,
        WalletDebitPurpose purpose,
        Guid transactionId,
        LedgerReferenceType referenceType,
        string? referenceId,
        UserId? performedByUserId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userWallet);
        ArgumentNullException.ThrowIfNull(systemAccount);

        var result = userWallet.Wallet.Debit(amount, purpose);

        if (result.GiftDebited > 0)
        {
            var userGiftDebitEntry = LedgerEntry.Create(
                userWallet.Account.Id,
                -result.GiftDebited,
                transactionId,
                referenceType,
                referenceId,
                performedByUserId,
                LedgerBalanceType.Gift);

            var systemGiftCreditEntry = LedgerEntry.Create(
                systemAccount.Id,
                result.GiftDebited,
                transactionId,
                referenceType,
                referenceId,
                performedByUserId,
                LedgerBalanceType.Gift);

            await ledgerRepository.AddAsync(userGiftDebitEntry, ct);
            await ledgerRepository.AddAsync(systemGiftCreditEntry, ct);
        }

        if (result.RegularDebited > 0)
        {
            var userRegularDebitEntry = LedgerEntry.Create(
                userWallet.Account.Id,
                -result.RegularDebited,
                transactionId,
                referenceType,
                referenceId,
                performedByUserId,
                LedgerBalanceType.Regular);

            var systemRegularCreditEntry = LedgerEntry.Create(
                systemAccount.Id,
                result.RegularDebited,
                transactionId,
                referenceType,
                referenceId,
                performedByUserId,
                LedgerBalanceType.Regular);

            await ledgerRepository.AddAsync(userRegularDebitEntry, ct);
            await ledgerRepository.AddAsync(systemRegularCreditEntry, ct);
        }

        walletRepository.Update(userWallet.Wallet);

        return result;
    }

    public async Task<WalletCreditResult> PostCreditAsync(
        ProvisionedUserWallet userWallet,
        Account systemAccount,
        decimal amount,
        LedgerBalanceType balanceType,
        Guid transactionId,
        LedgerReferenceType referenceType,
        string? referenceId,
        UserId? performedByUserId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userWallet);
        ArgumentNullException.ThrowIfNull(systemAccount);

        var result = userWallet.Wallet.Credit(amount, balanceType);

        var userCreditEntry = LedgerEntry.Create(
            userWallet.Account.Id,
            amount,
            transactionId,
            referenceType,
            referenceId,
            performedByUserId,
            balanceType);

        var systemDebitEntry = LedgerEntry.Create(
            systemAccount.Id,
            -amount,
            transactionId,
            referenceType,
            referenceId,
            performedByUserId,
            balanceType);

        await ledgerRepository.AddAsync(userCreditEntry, ct);
        await ledgerRepository.AddAsync(systemDebitEntry, ct);

        walletRepository.Update(userWallet.Wallet);

        return result;
    }
}

