using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using ByeMoney.Domain.Modules.Settings;
using System.Globalization;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;

public class CreateTopUpRequestCommandHandler : IRequestHandler<CreateTopUpRequestCommand, CreateTopUpRequestResponse>
{
    private readonly ITopUpRequestRepository _topUpRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemSettingRepository _settings;

    public CreateTopUpRequestCommandHandler(
        ITopUpRequestRepository topUpRequestRepository,
        IUnitOfWork unitOfWork,
        ISystemSettingRepository settings)
    {
        _topUpRequestRepository = topUpRequestRepository;
        _unitOfWork = unitOfWork;
        _settings = settings;
    }

    public async Task<CreateTopUpRequestResponse> Handle(CreateTopUpRequestCommand request, CancellationToken cancellationToken)
    {
        TopUpRequest topUp;
        if (request.PaymentMethod == PaymentMethod.Gateway)
        {
            var setting = await _settings.GetByKeyAsync(SystemSettingConstants.Keys.RialToNoor, cancellationToken);
            if (setting is null || !decimal.TryParse(setting.Value, NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var rate) || rate <= 0 || decimal.Truncate(rate) != rate)
                throw new DomainException(ApplicationErrors.TopUpRequest_InvalidRate);
            topUp = TopUpRequest.CreateGateway(new UserId(request.UserId), request.Amount, rate, request.PendingItems);
        }
        else
        {
            topUp = TopUpRequest.Create(new UserId(request.UserId), request.Amount,
                request.PaymentMethod, request.ExternalTransactionId, pendingItems: request.PendingItems);
        }

        await _topUpRequestRepository.AddAsync(topUp, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateTopUpRequestResponse(topUp.Id.Value, topUp.ClientReferenceCode, topUp.AmountRial ?? 0m);
    }
}

