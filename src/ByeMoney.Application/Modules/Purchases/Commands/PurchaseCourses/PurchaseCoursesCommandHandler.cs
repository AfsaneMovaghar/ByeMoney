using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Purchases.Services;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourses;

public class PurchaseCoursesCommandHandler : IRequestHandler<PurchaseCoursesCommand, Result<PurchaseCoursesResponse>>
{
    private readonly ISystemSettingRepository _settings;
    private readonly IUserRepository _userRepository;
    private readonly ITarhElahiIntegrationClient _tarhElahiClient;
    private readonly IUserWalletProvisioningService _walletProvisioningService;
    private readonly IWalletRepository _walletRepository;
    private readonly IRepository<LedgerEntry, LedgerEntryId> _ledgerRepository;
    private readonly ICoursePurchaseRepository _coursePurchaseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoursePurchaseNotifier _notifier;

    public PurchaseCoursesCommandHandler(
        IUserRepository userRepository,
        ITarhElahiIntegrationClient tarhElahiClient,
        IUserWalletProvisioningService walletProvisioningService,
        IWalletRepository walletRepository,
        IRepository<LedgerEntry, LedgerEntryId> ledgerRepository,
        ICoursePurchaseRepository coursePurchaseRepository,
        IUnitOfWork unitOfWork,
        ICoursePurchaseNotifier notifier,
        ISystemSettingRepository settings)
    {
        _settings = settings;
        _userRepository = userRepository;
        _tarhElahiClient = tarhElahiClient;
        _walletProvisioningService = walletProvisioningService;
        _walletRepository = walletRepository;
        _ledgerRepository = ledgerRepository;
        _coursePurchaseRepository = coursePurchaseRepository;
        _unitOfWork = unitOfWork;
        _notifier = notifier;
    }

    public async Task<Result<PurchaseCoursesResponse>> Handle(PurchaseCoursesCommand request, CancellationToken cancellationToken)
    {
        var buyerUserId = new UserId(request.BuyerUserId);
        var user = (await _userRepository.GetByIdAsync(buyerUserId, cancellationToken))!;

        var purchaseList = new List<(CoursePurchase Purchase, TarhElahiCourseDto Course, ProductSnapshot Snapshot, decimal PriceInNoor)>();

        var referenceRate = await PurchaseReferenceRateReader.ReadAsync(_settings, cancellationToken);

        foreach (var courseId in request.ExternalCourseIds)
        {
            var course = (await _tarhElahiClient.GetCourseAsync(courseId, cancellationToken))!;
            var priceInNoor = request.IsFree ? 0m : course.PriceNoor;

            var snapshot = ProductSnapshot.Create(
                course.ExternalId,
                course.Title,
                priceInNoor,
                course.Source,
                request.IsFree,
                referenceRialPerNoorAtPurchaseTime: referenceRate);

            var purchase = CoursePurchase.Create(buyerUserId, snapshot, request.IsFree, request.FreeReason);
            await _coursePurchaseRepository.AddAsync(purchase, cancellationToken);

            purchaseList.Add((purchase, course, snapshot, priceInNoor));
        }

        var transactionId = Guid.NewGuid();

        if (request.IsFree)
        {
            foreach (var item in purchaseList)
            {
                item.Purchase.MarkDebited(transactionId);
                _coursePurchaseRepository.Update(item.Purchase);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var provisioned = await _walletProvisioningService.GetOrCreateUserWalletAsync(buyerUserId, cancellationToken);
            await ExecuteFinancialTransactionAsync(provisioned, purchaseList, transactionId, cancellationToken);
        }

        foreach (var item in purchaseList)
        {
            await _notifier.NotifyAsync(item.Purchase, user.ExternalUserId, item.Snapshot, cancellationToken);
        }

        var items = purchaseList.Select(p => new PurchasedCourseItemResponse(
            p.Purchase.Id.Value,
            p.Course.ExternalId,
            p.Course.Title,
            p.PriceInNoor,
            p.Purchase.Status.ToString())).ToList();

        var response = new PurchaseCoursesResponse(
            transactionId,
            purchaseList.Sum(p => p.PriceInNoor),
            items,
            DateTime.UtcNow);

        return Result<PurchaseCoursesResponse>.Success(response);
    }

    private async Task ExecuteFinancialTransactionAsync(
        ProvisionedUserWallet provisioned,
        List<(CoursePurchase Purchase, TarhElahiCourseDto Course, ProductSnapshot Snapshot, decimal PriceInNoor)> purchaseList,
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var totalPriceInNoor = purchaseList.Sum(p => p.PriceInNoor);
        provisioned.Wallet.ApplyDebit(totalPriceInNoor);
        _walletRepository.Update(provisioned.Wallet);

        var systemAccount = await _walletProvisioningService.GetOrCreateSystemAccountAsync(cancellationToken);

        foreach (var item in purchaseList)
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

