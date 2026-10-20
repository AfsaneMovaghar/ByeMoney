using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.ReopenGatewayReview;

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
