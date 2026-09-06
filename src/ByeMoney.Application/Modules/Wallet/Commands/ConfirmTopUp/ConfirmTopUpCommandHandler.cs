using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using MediatR;
using WalletEntity = ByeMoney.Domain.Modules.Wallet.Wallets.Wallet;

namespace ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;

public class ConfirmTopUpCommandHandler : IRequestHandler<ConfirmTopUpCommand, Result>
{
    private readonly ITopUpRequestRepository _topUpRequestRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly IRepository<LedgerEntry, LedgerEntryId> _ledgerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmTopUpCommandHandler(
        ITopUpRequestRepository topUpRequestRepository,
        IAccountRepository accountRepository,
        IWalletRepository walletRepository,
        IRepository<LedgerEntry, LedgerEntryId> ledgerRepository,
        IUnitOfWork unitOfWork)
    {
        _topUpRequestRepository = topUpRequestRepository;
        _accountRepository = accountRepository;
        _walletRepository = walletRepository;
        _ledgerRepository = ledgerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ConfirmTopUpCommand request, CancellationToken cancellationToken)
    {
        var topUp = await _topUpRequestRepository.GetByIdAsync(request.TopUpRequestId, cancellationToken);
        if (topUp is null)
        {
            return Result.NotFound(string.Format(ApplicationErrors.TopUpRequest_NotFound, request.TopUpRequestId.Value));
        }

        if (request.ConfirmedAmount != topUp.Amount)
        {
            return Result.Failure(string.Format(ApplicationErrors.TopUpRequest_AmountMismatch, request.ConfirmedAmount, topUp.Amount));
        }

        var wasPending = topUp.Status == TopUpStatus.Pending;
        var confirmResult = topUp.Confirm(request.ExternalTransactionId);
        if (confirmResult.IsFailure)
        {
            return confirmResult;
        }

        // If Confirm() succeeds AND first-time confirmation -> write two balanced LedgerEntry records & credit wallet
        if (wasPending)
        {
            await ProcessFirstTimeConfirmationAsync(topUp, cancellationToken);
        }

        // If idempotent no-op (already Confirmed with same ExternalTransactionId) -> return Success without writing LedgerEntries
        return Result.Success();
    }

    private async Task ProcessFirstTimeConfirmationAsync(TopUpRequest topUp, CancellationToken cancellationToken)
    {
        var userAccount = await GetOrCreateUserAccountAsync(topUp.UserId, cancellationToken);
        var systemAccount = await GetOrCreateSystemAccountAsync(cancellationToken);

        await RecordBalancedLedgerEntriesAsync(userAccount.Id, systemAccount.Id, topUp, cancellationToken);
        await CreditUserWalletAsync(userAccount.Id, topUp.UserId, topUp.Amount, cancellationToken);

        _topUpRequestRepository.Update(topUp);

        // Persist all changes in a single UnitOfWork/transaction
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Account> GetOrCreateUserAccountAsync(UserId userId, CancellationToken cancellationToken)
    {
        var userAccount = await _accountRepository.GetByUserIdAsync(userId, cancellationToken);
        if (userAccount is null)
        {
            userAccount = Account.CreateUserAccount(userId);
            await _accountRepository.AddAsync(userAccount, cancellationToken);
        }

        return userAccount;
    }

    private async Task<Account> GetOrCreateSystemAccountAsync(CancellationToken cancellationToken)
    {
        var systemAccount = await _accountRepository.GetSystemAccountAsync(cancellationToken);
        if (systemAccount is null)
        {
            systemAccount = Account.CreateSystemAccount();
            await _accountRepository.AddAsync(systemAccount, cancellationToken);
        }

        return systemAccount;
    }

    private async Task RecordBalancedLedgerEntriesAsync(
        AccountId userAccountId,
        AccountId systemAccountId,
        TopUpRequest topUp,
        CancellationToken cancellationToken)
    {
        var transactionId = Guid.NewGuid();
        var userLedgerEntry = LedgerEntry.Create(
            userAccountId,
            topUp.Amount, // بستانکار (+Amount)
            transactionId,
            topUp.Id.ToString());

        var systemLedgerEntry = LedgerEntry.Create(
            systemAccountId,
            -topUp.Amount, // بدهکار (-Amount)
            transactionId,
            topUp.Id.ToString());

        await _ledgerRepository.AddAsync(userLedgerEntry, cancellationToken);
        await _ledgerRepository.AddAsync(systemLedgerEntry, cancellationToken);
    }

    private async Task CreditUserWalletAsync(
        AccountId accountId,
        UserId userId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByAccountIdAsync(accountId, cancellationToken);
        if (wallet is null)
        {
            wallet = WalletEntity.Create(accountId, userId);
            await _walletRepository.AddAsync(wallet, cancellationToken);
        }

        wallet.ApplyCredit(amount);
        _walletRepository.Update(wallet);
    }
}

