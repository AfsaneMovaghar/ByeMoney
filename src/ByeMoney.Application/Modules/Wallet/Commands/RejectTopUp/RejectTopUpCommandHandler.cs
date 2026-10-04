using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.RejectTopUp;

public class RejectTopUpCommandHandler(ITopUpRejectionService rejectionService) : IRequestHandler<RejectTopUpCommand, Result>
{
    public async Task<Result> Handle(RejectTopUpCommand request, CancellationToken cancellationToken)
        => await rejectionService.RejectAsync(request.TopUpRequestId, request.Reason, cancellationToken);
}
