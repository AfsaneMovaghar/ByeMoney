using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.OpenGatewayReview;

public sealed record OpenGatewayReviewCommand(string ClientReferenceCode, string CaseId, string ReasonCode)
    : IRequest<Result<GatewayReviewState>>;

public sealed class OpenGatewayReviewCommandHandler(IGatewayTopUpService service)
    : IRequestHandler<OpenGatewayReviewCommand, Result<GatewayReviewState>>
{
    public Task<Result<GatewayReviewState>> Handle(OpenGatewayReviewCommand request, CancellationToken ct)
        => service.OpenReviewAsync(request.ClientReferenceCode, request.CaseId, request.ReasonCode, ct);
}
