using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.RejectTopUp;

public class RejectTopUpCommandHandler : IRequestHandler<RejectTopUpCommand, bool>
{
    private readonly ITopUpRequestRepository _topUpRequestRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RejectTopUpCommandHandler(
        ITopUpRequestRepository topUpRequestRepository,
        IUnitOfWork unitOfWork)
    {
        _topUpRequestRepository = topUpRequestRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(RejectTopUpCommand request, CancellationToken cancellationToken)
    {
        var topUpId = new TopUpRequestId(request.TopUpRequestId);
        var topUp = await _topUpRequestRepository.GetByIdAsync(topUpId, cancellationToken);

        if (topUp is null)
            throw new NotFoundException(nameof(TopUpRequest), request.TopUpRequestId);

        topUp.Reject(request.Reason);
        _topUpRequestRepository.Update(topUp);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

