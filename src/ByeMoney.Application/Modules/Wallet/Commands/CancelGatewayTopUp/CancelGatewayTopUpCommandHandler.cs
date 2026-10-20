using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.CancelGatewayTopUp;

public sealed class CancelGatewayTopUpCommandHandler(IGatewayTopUpService gatewayTopUpService)
    : IRequestHandler<CancelGatewayTopUpCommand, Result>
{
    public Task<Result> Handle(CancelGatewayTopUpCommand request, CancellationToken ct)
        => gatewayTopUpService.CancelAsync(request, ct);
}

