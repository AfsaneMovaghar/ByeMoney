using ByeMoney.Domain.Common;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.CancelGatewayTopUp;

public sealed record CancelGatewayTopUpCommand(string ClientReferenceCode) : IRequest<Result>;
