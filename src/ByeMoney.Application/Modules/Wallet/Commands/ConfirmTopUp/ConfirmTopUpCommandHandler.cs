using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
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
            return Result.NotFound($"Top-up request with ID '{request.TopUpRequestId.Value}' was not found.");
        }

        // 2. If ConfirmedAmount != TopUpRequest.Amount -> Result.Failure, checked BEFORE calling Confirm().
        if (request.ConfirmedAmount != topUp.Amount)
        {
            return Result.Failure($"Confirmed amount ({request.ConfirmedAmount}) does not match top-up request amount ({topUp.Amount}).");
        }

        // 3. Call topUpRequest.Confirm(externalTransactionId). If it returns Failure, propagate failure.
        var wasPending = topUp.Status == TopUpStatus.Pending;
        var confirmResult = topUp.Confirm(request.ExternalTransactionId);
        if (confirmResult.IsFailure)
        {
            return confirmResult;
        }

        // 4. If Confirm() succeeds AND first-time confirmation -> write two balanced LedgerEntry records
        if (wasPending)
        {
            var userAccount = await _accountRepository.GetByUserIdAsync(topUp.UserId, cancellationToken);
            if (userAccount is null)
            {
                userAccount = Account.CreateUserAccount(topUp.UserId);
                await _accountRepository.AddAsync(userAccount, cancellationToken);
            }

            var systemAccount = await _accountRepository.GetSystemAccountAsync(cancellationToken);
            if (systemAccount is null)
            {
                systemAccount = Account.CreateSystemAccount();
                await _accountRepository.AddAsync(systemAccount, cancellationToken);
            }

            var transactionId = Guid.NewGuid();
            var userLedgerEntry = LedgerEntry.Create(
                userAccount.Id,
                topUp.Amount, // بستانکار (+Amount)
                transactionId,
                topUp.Id.ToString());

            var systemLedgerEntry = LedgerEntry.Create(
                systemAccount.Id,
                -topUp.Amount, // بدهکار (-Amount)
                transactionId,
                topUp.Id.ToString());

            await _ledgerRepository.AddAsync(userLedgerEntry, cancellationToken);
            await _ledgerRepository.AddAsync(systemLedgerEntry, cancellationToken);

            var wallet = await _walletRepository.GetByAccountIdAsync(userAccount.Id, cancellationToken);
            if (wallet is null)
            {
                wallet = WalletEntity.Create(userAccount.Id, topUp.UserId);
                await _walletRepository.AddAsync(wallet, cancellationToken);
            }

            wallet.ApplyCredit(topUp.Amount);
            _walletRepository.Update(wallet);
            _topUpRequestRepository.Update(topUp);

            // 6. Persist all changes in a single UnitOfWork/transaction
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // 5. If idempotent no-op (already Confirmed with same ExternalTransactionId) -> return Success without writing LedgerEntries
        return Result.Success();
    }
}

