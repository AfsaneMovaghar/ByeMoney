using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.ResolveGatewayReview;

public sealed record ResolveGatewayReviewCommand(string ClientReferenceCode, string CaseId,
    string OutcomeCode, string? ResolutionFinancialReferenceId) : IRequest<Result<GatewayReviewState>>;

public sealed class ResolveGatewayReviewCommandHandler(IGatewayTopUpService service)
    : IRequestHandler<ResolveGatewayReviewCommand, Result<GatewayReviewState>>
{
    public Task<Result<GatewayReviewState>> Handle(ResolveGatewayReviewCommand request, CancellationToken ct)
        => service.ResolveReviewAsync(request.ClientReferenceCode, request.CaseId,
            request.OutcomeCode, request.ResolutionFinancialReferenceId, ct);
}
