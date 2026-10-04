using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.RecordGatewayResult;

public sealed class RecordGatewayResultCommandHandler(IGatewayTopUpService service)
    : IRequestHandler<RecordGatewayResultCommand, Result>
{
    public Task<Result> Handle(RecordGatewayResultCommand request, CancellationToken ct)
        => service.RecordResultAsync(request, ct);
}
