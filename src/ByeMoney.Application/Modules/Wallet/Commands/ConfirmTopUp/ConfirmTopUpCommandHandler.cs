using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Events;
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
    private readonly IUserWalletProvisioningService _walletProvisioningService;
    private readonly IWalletRepository _walletRepository;
    private readonly IRepository<LedgerEntry, LedgerEntryId> _ledgerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    public ConfirmTopUpCommandHandler(
        ITopUpRequestRepository topUpRequestRepository,
        IUserWalletProvisioningService walletProvisioningService,
        IWalletRepository walletRepository,
        IRepository<LedgerEntry, LedgerEntryId> ledgerRepository,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
        _topUpRequestRepository = topUpRequestRepository;
        _walletProvisioningService = walletProvisioningService;
        _walletRepository = walletRepository;
        _ledgerRepository = ledgerRepository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
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

            if (topUp.PendingItemType.HasValue &&
                !string.IsNullOrWhiteSpace(topUp.PendingItemExternalId) &&
                topUp.PendingPriceSnapshot.HasValue &&
                topUp.PendingRateSnapshot.HasValue)
            {
                await _publisher.Publish(new TopUpConfirmed(
                    topUp.Id,
                    topUp.UserId,
                    topUp.Amount,
                    topUp.PendingItemType.Value,
                    topUp.PendingItemExternalId,
                    topUp.PendingPriceSnapshot.Value,
                    topUp.PendingRateSnapshot.Value), cancellationToken);
            }
        }

        // If idempotent no-op (already Confirmed with same ExternalTransactionId) -> return Success without writing LedgerEntries
        return Result.Success();
    }

    private async Task ProcessFirstTimeConfirmationAsync(TopUpRequest topUp, CancellationToken cancellationToken)
    {
        var provisioned = await _walletProvisioningService.GetOrCreateUserWalletAsync(topUp.UserId, cancellationToken);
        var systemAccount = await _walletProvisioningService.GetOrCreateSystemAccountAsync(cancellationToken);

        await RecordBalancedLedgerEntriesAsync(provisioned.Account.Id, systemAccount.Id, topUp, cancellationToken);
        CreditUserWallet(provisioned.Wallet, topUp.Amount);

        _topUpRequestRepository.Update(topUp);

        // Persist all changes in a single UnitOfWork/transaction
        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
            LedgerReferenceType.TopUp,
            topUp.Id.ToString());

        var systemLedgerEntry = LedgerEntry.Create(
            systemAccountId,
            -topUp.Amount, // بدهکار (-Amount)
            transactionId,
            LedgerReferenceType.TopUp,
            topUp.Id.ToString());

        await _ledgerRepository.AddAsync(userLedgerEntry, cancellationToken);
        await _ledgerRepository.AddAsync(systemLedgerEntry, cancellationToken);
    }

    private void CreditUserWallet(WalletEntity wallet, decimal amount)
    {
        wallet.ApplyCredit(amount);
        _walletRepository.Update(wallet);
    }
}

