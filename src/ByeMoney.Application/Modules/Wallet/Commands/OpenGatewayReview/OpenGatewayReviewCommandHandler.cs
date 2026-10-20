using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.OpenGatewayReview;

public sealed class OpenGatewayReviewCommandHandler(IGatewayTopUpService service)
    : IRequestHandler<OpenGatewayReviewCommand, Result<GatewayReviewState>>
{
    public Task<Result<GatewayReviewState>> Handle(OpenGatewayReviewCommand request, CancellationToken ct)
        => service.OpenReviewAsync(request.ClientReferenceCode, request.CaseId, request.ReasonCode, ct);
}
