using ByeMoney.Application.Modules.Wallet.Commands.CancelGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;
using ByeMoney.Domain.Common;

namespace ByeMoney.Application.Modules.Wallet.Interfaces;

public interface IGatewayTopUpService
{
    Task<GatewayConfirmationOutcome> ConfirmAsync(ConfirmGatewayTopUpCommand command, CancellationToken ct = default);
    Task<Result> CancelAsync(CancelGatewayTopUpCommand command, CancellationToken ct = default);
}

