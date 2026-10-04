using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using FluentValidation;
using MediatR;

namespace ByeMoney.Application.Modules.Wallet.Commands.ReopenGatewayReview;

public sealed record ReopenGatewayReviewCommand(string ClientReferenceCode, string CaseId,
    string EvidenceId, string? Note) : IRequest<Result<GatewayReviewState>>;

public sealed class ReopenGatewayReviewCommandHandler(IGatewayTopUpService service)
    : IRequestHandler<ReopenGatewayReviewCommand, Result<GatewayReviewState>>
{
    public Task<Result<GatewayReviewState>> Handle(ReopenGatewayReviewCommand request, CancellationToken ct)
        => service.ReopenReviewAsync(request.ClientReferenceCode, request.CaseId, request.EvidenceId, request.Note, ct);
}

public sealed class ReopenGatewayReviewCommandValidator : AbstractValidator<ReopenGatewayReviewCommand>
{
    public ReopenGatewayReviewCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.ClientReferenceCode).NotEmpty().MaximumLength(32).WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
        RuleFor(x => x.CaseId).NotEmpty().MaximumLength(100).WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
        RuleFor(x => x.EvidenceId).NotEmpty().MaximumLength(100).WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
        RuleFor(x => x.Note).MaximumLength(2000).WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
    }
}
