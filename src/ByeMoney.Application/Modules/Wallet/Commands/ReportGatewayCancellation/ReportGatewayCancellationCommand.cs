using ByeMoney.Domain.Common;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.ReportGatewayCancellation;

public sealed record ReportGatewayCancellationCommand(string ClientReferenceCode) : IRequest<Result>;
