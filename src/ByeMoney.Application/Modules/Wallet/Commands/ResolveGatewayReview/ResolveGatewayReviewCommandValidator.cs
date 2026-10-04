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
            FinancialReviewOutcomeCodes.ReversedRejected or FinancialReviewOutcomeCodes.ManualRefund)
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
    }
}
