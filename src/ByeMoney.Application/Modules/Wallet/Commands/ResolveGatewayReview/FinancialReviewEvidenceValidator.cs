using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.ResolveGatewayReview;

public sealed class FinancialReviewEvidenceValidator : AbstractValidator<FinancialReviewEvidence>
{
    public FinancialReviewEvidenceValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.OperationId).NotEmpty().WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
        RuleFor(x => x.ExpectedRevision).GreaterThan(0).WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
        RuleFor(x => x.CheckedReportDate).Must(x => x != default && x <= DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3.5)))
            .WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
        RuleFor(x => x.MatchingDepositFound).NotNull().WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
        RuleFor(x => x.Note).MaximumLength(2000).WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
        RuleFor(x => x.DepositReference).MaximumLength(100).WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
        RuleFor(x => x.ManualRefundReference).MaximumLength(100).WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
        When(x => x.MatchingDepositFound == true, () =>
        {
            RuleFor(x => x.DepositReference).NotEmpty().WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
            RuleFor(x => x.DepositDate).NotNull().Must((x, date) => date.HasValue && date.Value != default && date.Value <= x.CheckedReportDate)
                .WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
            RuleFor(x => x.DepositAmountRial).NotNull().GreaterThan(0).Must(x => x.HasValue && decimal.Truncate(x.Value) == x.Value)
                .WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
        });
        When(x => x.MatchingDepositFound == false, () =>
        {
            RuleFor(x => x.DepositReference).Null().WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
            RuleFor(x => x.DepositDate).Null().WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
            RuleFor(x => x.DepositAmountRial).Null().WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
        });
    }
}
