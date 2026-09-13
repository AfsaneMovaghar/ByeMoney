using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Accounts;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourse;

public class PurchaseCourseCommandHandler : IRequestHandler<PurchaseCourseCommand, Result<PurchaseCourseResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITarhElahiIntegrationClient _tarhElahiClient;
    private readonly ISystemSettingRepository _settingRepository;
    private readonly IUserWalletProvisioningService _walletProvisioningService;
    private readonly IWalletRepository _walletRepository;
    private readonly IRepository<LedgerEntry, LedgerEntryId> _ledgerRepository;
    private readonly ICoursePurchaseRepository _coursePurchaseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoursePurchaseNotifier _notifier;

    public PurchaseCourseCommandHandler(
        IUserRepository userRepository,
        ITarhElahiIntegrationClient tarhElahiClient,
        ISystemSettingRepository settingRepository,
        IUserWalletProvisioningService walletProvisioningService,
        IWalletRepository walletRepository,
        IRepository<LedgerEntry, LedgerEntryId> ledgerRepository,
        ICoursePurchaseRepository coursePurchaseRepository,
        IUnitOfWork unitOfWork,
        ICoursePurchaseNotifier notifier)
    {
        _userRepository = userRepository;
        _tarhElahiClient = tarhElahiClient;
        _settingRepository = settingRepository;
        _walletProvisioningService = walletProvisioningService;
        _walletRepository = walletRepository;
        _ledgerRepository = ledgerRepository;
        _coursePurchaseRepository = coursePurchaseRepository;
        _unitOfWork = unitOfWork;
        _notifier = notifier;
    }

    public async Task<Result<PurchaseCourseResponse>> Handle(PurchaseCourseCommand request, CancellationToken cancellationToken)
    {
        var buyerUserId = new UserId(request.BuyerUserId);
        var user = (await _userRepository.GetByIdAsync(buyerUserId, cancellationToken))!;
        var course = (await _tarhElahiClient.GetCourseAsync(request.ExternalCourseId, cancellationToken))!;
        var conversionRate = await _settingRepository.GetRialToNoorConversionRateAsync(cancellationToken);
        var priceInNoor = course.PriceRial / conversionRate;

        var snapshot = ProductSnapshot.Create(
            course.ExternalId,
            course.Title,
            course.PriceRial,
            conversionRate,
            priceInNoor,
            course.Source);

        var purchase = CoursePurchase.Create(buyerUserId, snapshot);
        await _coursePurchaseRepository.AddAsync(purchase, cancellationToken);

        var provisioned = await _walletProvisioningService.GetOrCreateUserWalletAsync(buyerUserId, cancellationToken);
        var transactionId = Guid.NewGuid();
        await ExecuteFinancialTransactionAsync(provisioned, priceInNoor, transactionId, purchase, cancellationToken);

        await _notifier.NotifyAsync(purchase, user.ExternalUserId, course, snapshot, cancellationToken);

        var response = new PurchaseCourseResponse(
            purchase.Id.Value,
            course.ExternalId,
            course.Title,
            priceInNoor,
            purchase.Status.ToString(),
            snapshot.PurchasedAt);

        return Result<PurchaseCourseResponse>.Success(response);
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
