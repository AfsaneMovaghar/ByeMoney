using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.RejectTopUp;

public class RejectTopUpCommandHandler : IRequestHandler<RejectTopUpCommand, Result>
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

    public async Task<Result> Handle(RejectTopUpCommand request, CancellationToken cancellationToken)
    {
        var topUpId = new TopUpRequestId(request.TopUpRequestId);
        var topUp = await _topUpRequestRepository.GetByIdAsync(topUpId, cancellationToken);

        if (topUp is null)
            return Result.NotFound(string.Format(
                ApplicationErrors.TopUpRequest_NotFound, request.TopUpRequestId));

        topUp.Reject(request.Reason);
        _topUpRequestRepository.Update(topUp);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
