using ByeMoney.Application.Modules.Settings.Interfaces;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;

public class CreateTopUpRequestCommandHandler : IRequestHandler<CreateTopUpRequestCommand, CreateTopUpRequestResponse>
{
    private readonly ITopUpRequestRepository _topUpRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemSettingRepository _settings;
    private readonly ITarhElahiIntegrationClient _catalog;
    private readonly IWalletRepository _wallets;

    public CreateTopUpRequestCommandHandler(
        ITopUpRequestRepository topUpRequestRepository,
        IUnitOfWork unitOfWork,
        ISystemSettingRepository settings,
        ITarhElahiIntegrationClient catalog,
        IWalletRepository wallets)
    {
        _topUpRequestRepository = topUpRequestRepository;
        _unitOfWork = unitOfWork;
        _settings = settings;
        _catalog = catalog;
        _wallets = wallets;
    }

    public async Task<CreateTopUpRequestResponse> Handle(CreateTopUpRequestCommand request, CancellationToken cancellationToken)
    {
        var pendingItems = await BuildPendingItemsAsync(request.PendingItems, cancellationToken);
        TopUpRequest topUp;
        if (request.PaymentMethod == PaymentMethod.Gateway)
        {
            var amountNoor = request.AmountNoor;
            if (pendingItems.Count > 0)
            {
                var wallet = await _wallets.GetByUserIdAsync(new UserId(request.UserId), cancellationToken);
                amountNoor = decimal.Ceiling(pendingItems.Sum(i => i.PriceNoorSnapshot) - (wallet?.TotalBalance ?? 0m));
                if (amountNoor <= 0)
                    throw new DomainException(ApplicationErrors.TopUpRequest_AmountMustBeGreaterThanZero);
            }
            var rate = await _settings.GetRialToNoorConversionRateAsync(cancellationToken);
            if (rate <= 0 || decimal.Truncate(rate) != rate)
                throw new DomainException(ApplicationErrors.TopUpRequest_InvalidRate);
            topUp = TopUpRequest.CreateGateway(new UserId(request.UserId), amountNoor, rate, pendingItems);
        }
        else
        {
            topUp = TopUpRequest.Create(new UserId(request.UserId), request.AmountNoor,
                request.PaymentMethod, request.ExternalTransactionId, pendingItems: pendingItems);
        }

        await _topUpRequestRepository.AddAsync(topUp, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateTopUpRequestResponse(topUp.Id.Value, topUp.ClientReferenceCode, topUp.AmountRial ?? 0m);
    }

    private async Task<IReadOnlyList<PendingItemSnapshot>> BuildPendingItemsAsync(
        IReadOnlyList<PendingItemSnapshot>? items, CancellationToken ct)
    {
        var snapshots = new List<PendingItemSnapshot>();
        foreach (var item in items ?? [])
        {
            var course = await _catalog.GetCourseAsync(item.ExternalId, ct);
            if (course is null)
                throw new DomainException(ApplicationErrors.CoursePurchase_CourseNotFound);
            if (!course.Published || !course.Available)
                throw new DomainException(ApplicationErrors.CoursePurchase_CourseNotAvailable);
            if (course.PriceNoor <= 0)
                throw new DomainException(ApplicationErrors.TopUpRequest_PendingPriceMustBeGreaterThanZero);
            if (decimal.Truncate(course.PriceNoor) != course.PriceNoor)
                throw new DomainException(ApplicationErrors.CoursePurchase_PriceMustBeWholeNoor);
            snapshots.Add(new PendingItemSnapshot(item.ItemType, course.ExternalId, course.PriceNoor));
        }
        return snapshots;
    }
}

