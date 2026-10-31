using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.Constants;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Purchases.Services;
using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet.Ledgers;
using ByeMoney.Domain.Modules.Wallet.Wallets;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourses;

public class PurchaseCoursesCommandHandler : IRequestHandler<PurchaseCoursesCommand, Result<PurchaseCoursesResponse>>
{
    private readonly ISystemSettingRepository _settings;
    private readonly IUserRepository _userRepository;
    private readonly ITarhElahiIntegrationClient _tarhElahiClient;
    private readonly IUserWalletProvisioningService _walletProvisioningService;
    private readonly IWalletRepository _walletRepository;
    private readonly IWalletPostingService _walletPostingService;
    private readonly ICoursePurchaseRepository _coursePurchaseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoursePurchaseNotifier _notifier;

    public PurchaseCoursesCommandHandler(
        IUserRepository userRepository,
        ITarhElahiIntegrationClient tarhElahiClient,
        IUserWalletProvisioningService walletProvisioningService,
        IWalletRepository walletRepository,
        IWalletPostingService walletPostingService,
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
        _walletPostingService = walletPostingService;
        _coursePurchaseRepository = coursePurchaseRepository;
        _unitOfWork = unitOfWork;
        _notifier = notifier;
    }

    public async Task<Result<PurchaseCoursesResponse>> Handle(PurchaseCoursesCommand request, CancellationToken cancellationToken)
    {
        var buyerUserId = new UserId(request.BuyerUserId);
        var user = await _userRepository.GetByIdAsync(buyerUserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result<PurchaseCoursesResponse>.Conflict(
                ApplicationErrors.CoursePurchase_UserNotFound,
                CartErrorCodes.Conflict);
        }

        if (await _coursePurchaseRepository.HasPurchasedAnyAsync(buyerUserId, request.ExternalCourseIds, ct: cancellationToken))
        {
            return Result<PurchaseCoursesResponse>.Conflict(
                ApplicationErrors.CoursePurchase_AlreadyPurchased,
                PurchaseErrorCodes.AlreadyPurchased);
        }

        var purchaseList = new List<(CoursePurchase Purchase, TarhElahiCourseDto Course, ProductSnapshot Snapshot, decimal PriceInNoor)>();
        var referenceRate = await PurchaseReferenceRateReader.ReadAsync(_settings, cancellationToken);

        foreach (var courseId in request.ExternalCourseIds)
        {
            var course = await _tarhElahiClient.GetCourseAsync(courseId, cancellationToken);
            if (course is null)
            {
                return Result<PurchaseCoursesResponse>.Failure(
                    ApplicationErrors.CoursePurchase_CourseNotFound,
                    CartErrorCodes.NotFound);
            }

            var priceInNoor = request.IsFree ? 0m : course.PriceNoor;

            var snapshot = ProductSnapshot.Create(
                course.ExternalId,
                course.Title,
                priceInNoor,
                course.Source,
                request.IsFree,
                referenceRialPerNoorAtPurchaseTime: referenceRate);

            var purchase = CoursePurchase.Create(buyerUserId, snapshot, request.IsFree, request.FreeReason);
            purchaseList.Add((purchase, course, snapshot, priceInNoor));
        }

        var transactionId = Guid.NewGuid();

        var persistenceResult = await _coursePurchaseRepository.ExecutePurchaseTransactionAsync(
            buyerUserId,
            async () =>
            {
                foreach (var item in purchaseList)
                {
                    await _coursePurchaseRepository.AddAsync(item.Purchase, cancellationToken);
                }

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
            },
            cancellationToken);

        if (persistenceResult.IsFailure)
        {
            return Result<PurchaseCoursesResponse>.Conflict(
                persistenceResult.ErrorMessage ?? ApplicationErrors.CoursePurchase_AlreadyPurchased,
                persistenceResult.ErrorCode ?? PurchaseErrorCodes.AlreadyPurchased);
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
        var systemAccount = await _walletProvisioningService.GetOrCreateSystemAccountAsync(cancellationToken);

        foreach (var item in purchaseList)
        {
            if (item.PriceInNoor > 0)
            {
                await _walletPostingService.PostDebitAsync(
                    provisioned,
                    systemAccount,
                    item.PriceInNoor,
                    WalletDebitPurpose.Purchase,
                    transactionId,
                    LedgerReferenceType.Purchase,
                    item.Purchase.Id.ToString(),
                    provisioned.Wallet.UserId,
                    cancellationToken);
            }

            item.Purchase.MarkDebited(transactionId);
            _coursePurchaseRepository.Update(item.Purchase);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

