using ByeMoney.Application.Resources;
using ByeMoney.Application.Modules.Wallet.Constants;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.ResolveGatewayReview;

public sealed class ResolveGatewayReviewCommandValidator : AbstractValidator<ResolveGatewayReviewCommand>
{
    public ResolveGatewayReviewCommandValidator()
    {
        RuleFor(x => x.ClientReferenceCode).Cascade(CascadeMode.Stop).NotEmpty()
            .WithMessage(ApplicationErrors.TopUpRequest_ReviewReferenceRequired).MaximumLength(32)
            .WithMessage(ApplicationErrors.TopUpRequest_ClientReferenceCodeMaxLength);
        RuleFor(x => x.CaseId).Cascade(CascadeMode.Stop).NotEmpty()
            .WithMessage(ApplicationErrors.TopUpRequest_ReviewCaseRequired).MaximumLength(100)
            .WithMessage(ApplicationErrors.TopUpRequest_GatewayEventIdMaxLength);
        RuleFor(x => x.OutcomeCode).Cascade(CascadeMode.Stop).Must(x => x is
            FinancialReviewOutcomeCodes.PaidAndConfirmed or FinancialReviewOutcomeCodes.UnpaidRejected or
            FinancialReviewOutcomeCodes.ReversedRejected or FinancialReviewOutcomeCodes.ManualRefund or FinancialReviewOutcomeCodes.NoMatchingDeposit)
            .WithMessage(ApplicationErrors.TopUpRequest_ReviewOutcomeInvalid)
            .WithState(_ => new GatewayReviewValidationError(GatewayTopUpErrorCodes.ReviewOutcomeInvalid,
                ApplicationErrors.TopUpRequest_ReviewOutcomeInvalid));
        RuleFor(x => x.ResolutionFinancialReferenceId).Cascade(CascadeMode.Stop).MaximumLength(100)
            .WithMessage(ApplicationErrors.TopUpRequest_ReviewFinancialReferenceInvalid);
        When(x => x.OutcomeCode is FinancialReviewOutcomeCodes.PaidAndConfirmed or
            FinancialReviewOutcomeCodes.ReversedRejected, () =>
            RuleFor(x => x.ResolutionFinancialReferenceId).Cascade(CascadeMode.Stop).NotEmpty()
                .WithMessage(ApplicationErrors.TopUpRequest_ReviewFinancialReferenceInvalid));
        When(x => x.OutcomeCode == FinancialReviewOutcomeCodes.UnpaidRejected, () =>
            RuleFor(x => x.ResolutionFinancialReferenceId).Cascade(CascadeMode.Stop).Empty()
                .WithMessage(ApplicationErrors.TopUpRequest_ReviewFinancialReferenceInvalid));
        RuleFor(x => x.Evidence).Cascade(CascadeMode.Stop).NotNull()
            .WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
        When(x => x.Evidence is not null, () =>
        {
            RuleFor(x => x.Evidence!).SetValidator(new FinancialReviewEvidenceValidator());
            RuleFor(x => x).Cascade(CascadeMode.Stop).Must(x =>
                x.OutcomeCode is not (FinancialReviewOutcomeCodes.NoMatchingDeposit or FinancialReviewOutcomeCodes.UnpaidRejected) ||
                x.Evidence!.MatchingDepositFound == false && x.ResolutionFinancialReferenceId is null)
                .WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
            RuleFor(x => x).Cascade(CascadeMode.Stop).Must(x =>
                x.OutcomeCode != FinancialReviewOutcomeCodes.PaidAndConfirmed || x.Evidence!.MatchingDepositFound == true)
                .WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
            RuleFor(x => x).Cascade(CascadeMode.Stop).Must(x => x.OutcomeCode != FinancialReviewOutcomeCodes.ManualRefund ||
                !string.IsNullOrWhiteSpace(x.Evidence!.ManualRefundReference) && x.ResolutionFinancialReferenceId is null)
                .WithMessage(ApplicationErrors.TopUpRequest_ReviewEvidenceInvalid);
        });
    }
}

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
