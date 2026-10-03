using ByeMoney.Application.Modules.Wallet.Interfaces;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;

public sealed class ConfirmGatewayTopUpCommandHandler(IGatewayTopUpService gatewayTopUpService)
    : IRequestHandler<ConfirmGatewayTopUpCommand, GatewayConfirmationOutcome>
{
    public Task<GatewayConfirmationOutcome> Handle(ConfirmGatewayTopUpCommand request, CancellationToken ct)
        => gatewayTopUpService.ConfirmAsync(request, ct);
}

