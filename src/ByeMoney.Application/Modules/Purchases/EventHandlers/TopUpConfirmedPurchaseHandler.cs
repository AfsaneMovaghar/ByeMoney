using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.Events;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ByeMoney.Application.Modules.Purchases.EventHandlers;

public class TopUpConfirmedPurchaseHandler : INotificationHandler<TopUpConfirmed>
{
    private readonly IUserRepository _userRepository;
    private readonly ITarhElahiIntegrationClient _tarhElahiClient;
    private readonly ICoursePurchaseRepository _coursePurchaseRepository;
    private readonly IUserWalletProvisioningService _walletProvisioningService;
    private readonly IWalletRepository _walletRepository;
    private readonly IRepository<LedgerEntry, LedgerEntryId> _ledgerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoursePurchaseNotifier _notifier;
    private readonly ILogger<TopUpConfirmedPurchaseHandler> _logger;

    public TopUpConfirmedPurchaseHandler(
        IUserRepository userRepository,
        ITarhElahiIntegrationClient tarhElahiClient,
        ICoursePurchaseRepository coursePurchaseRepository,
        IUserWalletProvisioningService walletProvisioningService,
        IWalletRepository walletRepository,
        IRepository<LedgerEntry, LedgerEntryId> ledgerRepository,
        IUnitOfWork unitOfWork,
        ICoursePurchaseNotifier notifier,
        ILogger<TopUpConfirmedPurchaseHandler> logger)
    {
        _userRepository = userRepository;
        _tarhElahiClient = tarhElahiClient;
        _coursePurchaseRepository = coursePurchaseRepository;
        _walletProvisioningService = walletProvisioningService;
        _walletRepository = walletRepository;
        _ledgerRepository = ledgerRepository;
        _unitOfWork = unitOfWork;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Handle(TopUpConfirmed notification, CancellationToken cancellationToken)
    {
        if (notification.PendingItemType != PendingItemType.Course)
        {
            return;
        }

        // Idempotency: must not double-debit if the event is replayed or retried
        if (await _coursePurchaseRepository.ExistsByBuyerAndCourseAsync(notification.UserId, notification.PendingItemExternalId, cancellationToken))
        {
            _logger.LogInformation(
                "Auto-purchase on TopUpConfirmed skipped: Course {CourseId} already purchased for User {UserId}.",
                notification.PendingItemExternalId, notification.UserId);
            return;
        }

        var user = await _userRepository.GetByIdAsync(notification.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning(
                "Auto-purchase on TopUpConfirmed skipped: User {UserId} not found or inactive.",
                notification.UserId);
            return;
        }

        // Check only that PendingItemExternalId still exists and is purchasable (not deleted/unpublished) via TarhElahi.
        // Do NOT re-check or re-fetch price/rate - always honor PendingPriceSnapshot/PendingRateSnapshot as-is.
        var course = await _tarhElahiClient.GetCourseAsync(notification.PendingItemExternalId, cancellationToken);
        if (course is null || !course.Published || !course.Available)
        {
            _logger.LogWarning(
                "Auto-purchase on TopUpConfirmed skipped: Course {CourseId} not found, unpublished, or unavailable in TarhElahi. Wallet remains credited.",
                notification.PendingItemExternalId);
            return;
        }

        var priceRial = notification.PendingPriceSnapshot;
        var conversionRate = notification.PendingRateSnapshot;
        if (priceRial <= 0 || conversionRate <= 0)
        {
            _logger.LogError(
                "Auto-purchase on TopUpConfirmed aborted: Invalid frozen snapshot values (PriceRial: {PriceRial}, ConversionRate: {Rate}).",
                priceRial, conversionRate);
            return;
        }

        var priceInNoor = priceRial / conversionRate;

        var provisioned = await _walletProvisioningService.GetOrCreateUserWalletAsync(notification.UserId, cancellationToken);
        if (provisioned.Wallet.Balance < priceInNoor)
        {
            _logger.LogWarning(
                "Auto-purchase on TopUpConfirmed skipped: Wallet balance ({Balance}) insufficient for price in Noor ({PriceInNoor}). Wallet remains credited.",
                provisioned.Wallet.Balance, priceInNoor);
            return;
        }

        var snapshot = ProductSnapshot.Create(
            course.ExternalId,
            course.Title,
            priceRial,
            conversionRate,
            priceInNoor,
            course.Source);

        var purchase = CoursePurchase.Create(notification.UserId, snapshot);
        await _coursePurchaseRepository.AddAsync(purchase, cancellationToken);

        var transactionId = Guid.NewGuid();
        await ExecuteFinancialTransactionAsync(provisioned, priceInNoor, transactionId, purchase, cancellationToken);

        // Proceed through the existing delivery/outbox (I09) path
        await _notifier.NotifyAsync(purchase, user.ExternalUserId, course, snapshot, cancellationToken);

        _logger.LogInformation(
            "Auto-purchase on TopUpConfirmed executed successfully: PurchaseId {PurchaseId} for Course {CourseId} by User {UserId}.",
            purchase.Id.Value, course.ExternalId, notification.UserId);
    }

    private async Task ExecuteFinancialTransactionAsync(
        ProvisionedUserWallet provisioned,
        decimal priceInNoor,
        Guid transactionId,
        CoursePurchase purchase,
        CancellationToken cancellationToken)
    {
        provisioned.Wallet.ApplyDebit(priceInNoor);
        _walletRepository.Update(provisioned.Wallet);

        var systemAccount = await _walletProvisioningService.GetOrCreateSystemAccountAsync(cancellationToken);

        var buyerDebitEntry = LedgerEntry.Create(
            provisioned.Account.Id,
            -priceInNoor,
            transactionId,
            LedgerReferenceType.Purchase,
            purchase.Id.ToString());

        var systemCreditEntry = LedgerEntry.Create(
            systemAccount.Id,
            priceInNoor,
            transactionId,
            LedgerReferenceType.Purchase,
            purchase.Id.ToString());

        await _ledgerRepository.AddAsync(buyerDebitEntry, cancellationToken);
        await _ledgerRepository.AddAsync(systemCreditEntry, cancellationToken);

        purchase.MarkDebited(transactionId);
        _coursePurchaseRepository.Update(purchase);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

