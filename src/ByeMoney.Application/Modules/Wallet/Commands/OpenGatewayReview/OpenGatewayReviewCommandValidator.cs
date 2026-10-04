using ByeMoney.Application.Resources;
using ByeMoney.Application.Modules.Wallet.Constants;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.OpenGatewayReview;

public sealed class OpenGatewayReviewCommandValidator : AbstractValidator<OpenGatewayReviewCommand>
{
    public OpenGatewayReviewCommandValidator()
    {
        RuleFor(x => x.ClientReferenceCode).Cascade(CascadeMode.Stop).NotEmpty()
            .WithMessage(ApplicationErrors.TopUpRequest_ReviewReferenceRequired).MaximumLength(32)
            .WithMessage(ApplicationErrors.TopUpRequest_ClientReferenceCodeMaxLength);
        RuleFor(x => x.CaseId).Cascade(CascadeMode.Stop).NotEmpty()
            .WithMessage(ApplicationErrors.TopUpRequest_ReviewCaseRequired).MaximumLength(100)
            .WithMessage(ApplicationErrors.TopUpRequest_GatewayEventIdMaxLength);
        RuleFor(x => x.ReasonCode).Cascade(CascadeMode.Stop).Must(x => x is
            FinancialReviewReasonCodes.NoCallback or FinancialReviewReasonCodes.VerifyUnknown or
            FinancialReviewReasonCodes.ReverseUnknown or FinancialReviewReasonCodes.DeliveryUnknown or
            FinancialReviewReasonCodes.BankConflict)
            .WithMessage(ApplicationErrors.TopUpRequest_ReviewReasonInvalid)
            .WithState(_ => new GatewayReviewValidationError(GatewayTopUpErrorCodes.ReviewReasonInvalid,
                ApplicationErrors.TopUpRequest_ReviewReasonInvalid));
    }
}
