using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using MediatR;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;

namespace ByeMoney.Application.Modules.Wallet.Commands.ResolveGatewayReview;

public sealed record ResolveGatewayReviewCommand(string ClientReferenceCode, string CaseId,
    string OutcomeCode, string? ResolutionFinancialReferenceId, FinancialReviewEvidence? Evidence = null)
    : IRequest<Result<GatewayReviewState>>;

public sealed class ResolveGatewayReviewCommandHandler(IGatewayTopUpService service,
    ICurrentUserService currentUser, IUserRepository users)
    : IRequestHandler<ResolveGatewayReviewCommand, Result<GatewayReviewState>>
{
    public async Task<Result<GatewayReviewState>> Handle(ResolveGatewayReviewCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actorId)
            throw new UnauthorizedAccessException(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
        var actor = await users.GetByIdAsync(new UserId(actorId), ct);
        if (actor is null || !actor.IsActive)
            throw new UnauthorizedAccessException(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
        return await service.ResolveManualReviewAsync(request.ClientReferenceCode, request.CaseId,
            request.OutcomeCode, request.ResolutionFinancialReferenceId, request.Evidence!, actorId, actor.DisplayName, ct);
    }
}
