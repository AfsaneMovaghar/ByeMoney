using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.ReopenGatewayReview;

public sealed class ReopenGatewayReviewCommandHandler(IGatewayTopUpService service)
    : IRequestHandler<ReopenGatewayReviewCommand, Result<GatewayReviewState>>
{
    public Task<Result<GatewayReviewState>> Handle(ReopenGatewayReviewCommand request, CancellationToken ct)
        => service.ReopenReviewAsync(request.ClientReferenceCode, request.CaseId, request.EvidenceId, request.Note, ct);
}
