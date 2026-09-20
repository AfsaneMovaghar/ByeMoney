using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
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
        if (notification.PendingItems == null || notification.PendingItems.Count == 0)
        {
            return;
        }

        var courseItems = notification.PendingItems
            .Where(i => i.ItemType == PendingItemType.Course)
            .ToList();

        if (courseItems.Count == 0)
        {
            return;
        }

        var unpurchasedItems = new List<PendingItemSnapshot>();
        foreach (var item in courseItems)
        {
            // Idempotency: skip if already purchased
            if (await _coursePurchaseRepository.ExistsByBuyerAndCourseAsync(notification.UserId, item.ExternalId, cancellationToken))
            {
                _logger.LogInformation(
                    "Auto-purchase on TopUpConfirmed skipped: Course {CourseId} already purchased for User {UserId}.",
                    item.ExternalId, notification.UserId);
                continue;
            }

            unpurchasedItems.Add(item);
        }

        if (unpurchasedItems.Count == 0)
        {
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

        var itemsToPurchase = new List<(CoursePurchase Purchase, TarhElahiCourseDto Course, ProductSnapshot Snapshot, decimal PriceInNoor)>();

        foreach (var item in unpurchasedItems)
        {
            var course = await _tarhElahiClient.GetCourseAsync(item.ExternalId, cancellationToken);
            if (course is null || !course.Published || !course.Available)
            {
                _logger.LogWarning(
                    "Auto-purchase on TopUpConfirmed skipped: Course {CourseId} not found, unpublished, or unavailable in TarhElahi. Wallet remains credited.",
                    item.ExternalId);
                return;
            }

            var priceRial = item.PriceSnapshot;
            var conversionRate = item.RateSnapshot;
            if (priceRial <= 0 || conversionRate <= 0)
            {
                _logger.LogError(
                    "Auto-purchase on TopUpConfirmed aborted: Invalid frozen snapshot values for Course {CourseId} (PriceRial: {PriceRial}, ConversionRate: {Rate}).",
                    item.ExternalId, priceRial, conversionRate);
                return;
            }

            var priceInNoor = priceRial / conversionRate;

            var snapshot = ProductSnapshot.Create(
                course.ExternalId,
                course.Title,
                priceRial,
                conversionRate,
                priceInNoor,
                course.Source);

            var purchase = CoursePurchase.Create(notification.UserId, snapshot);
            itemsToPurchase.Add((purchase, course, snapshot, priceInNoor));
        }

        var totalPriceInNoor = itemsToPurchase.Sum(x => x.PriceInNoor);
        var provisioned = await _walletProvisioningService.GetOrCreateUserWalletAsync(notification.UserId, cancellationToken);
        if (provisioned.Wallet.Balance < totalPriceInNoor)
        {
            _logger.LogWarning(
                "Auto-purchase on TopUpConfirmed skipped: Wallet balance ({Balance}) insufficient for price in Noor ({PriceInNoor}). Wallet remains credited.",
                provisioned.Wallet.Balance, totalPriceInNoor);
            return;
        }

        foreach (var item in itemsToPurchase)
        {
            await _coursePurchaseRepository.AddAsync(item.Purchase, cancellationToken);
        }

        var transactionId = Guid.NewGuid();
        await ExecuteFinancialTransactionAsync(provisioned, itemsToPurchase, transactionId, cancellationToken);

        foreach (var item in itemsToPurchase)
        {
            await _notifier.NotifyAsync(item.Purchase, user.ExternalUserId, item.Course, item.Snapshot, cancellationToken);

            _logger.LogInformation(
                "Auto-purchase on TopUpConfirmed executed successfully: PurchaseId {PurchaseId} for Course {CourseId} by User {UserId}.",
                item.Purchase.Id.Value, item.Course.ExternalId, notification.UserId);
        }
    }

    private async Task ExecuteFinancialTransactionAsync(
        ProvisionedUserWallet provisioned,
        List<(CoursePurchase Purchase, TarhElahiCourseDto Course, ProductSnapshot Snapshot, decimal PriceInNoor)> itemsToPurchase,
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var totalPriceInNoor = itemsToPurchase.Sum(x => x.PriceInNoor);
        provisioned.Wallet.ApplyDebit(totalPriceInNoor);
        _walletRepository.Update(provisioned.Wallet);

        var systemAccount = await _walletProvisioningService.GetOrCreateSystemAccountAsync(cancellationToken);

        foreach (var item in itemsToPurchase)
        {
            var buyerDebitEntry = LedgerEntry.Create(
                provisioned.Account.Id,
                -item.PriceInNoor,
                transactionId,
                LedgerReferenceType.Purchase,
                item.Purchase.Id.ToString());

            var systemCreditEntry = LedgerEntry.Create(
                systemAccount.Id,
                item.PriceInNoor,
                transactionId,
                LedgerReferenceType.Purchase,
                item.Purchase.Id.ToString());

            await _ledgerRepository.AddAsync(buyerDebitEntry, cancellationToken);
            await _ledgerRepository.AddAsync(systemCreditEntry, cancellationToken);

            item.Purchase.MarkDebited(transactionId);
            _coursePurchaseRepository.Update(item.Purchase);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

