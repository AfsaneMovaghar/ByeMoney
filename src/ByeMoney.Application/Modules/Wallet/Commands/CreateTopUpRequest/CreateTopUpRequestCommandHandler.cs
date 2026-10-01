using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
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
        var rate = request.PaymentMethod == PaymentMethod.Gateway
            ? await _settings.GetRialToNoorConversionRateAsync(cancellationToken)
            : (decimal?)null;
        var topUp = TopUpRequest.Create(
            new UserId(request.UserId),
            request.Amount,
            request.PaymentMethod,
            request.ExternalTransactionId,
            pendingItems: request.PendingItems,
            rialPerNoor: rate);

        await _topUpRequestRepository.AddAsync(topUp, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateTopUpRequestResponse(topUp.Id.Value, topUp.ClientReferenceCode, topUp.AmountRial ?? 0m);
    }
}

