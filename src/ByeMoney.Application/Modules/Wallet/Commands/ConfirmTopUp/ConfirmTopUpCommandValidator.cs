using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;

public class ConfirmTopUpCommandValidator : AbstractValidator<ConfirmTopUpCommand>
{
    public ConfirmTopUpCommandValidator()
    {
        When(x => x.GatewayName is null, () =>
        {
            RuleFor(x => x.TopUpRequestId.Value).Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(ApplicationErrors.TopUpRequest_IdRequired);
            RuleFor(x => x.ConfirmedAmountNoor).Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithMessage(ApplicationErrors.TopUpRequest_ConfirmedAmountMustBeGreaterThanZero);
        });

        When(x => x.GatewayName is not null, () =>
        {
            RuleFor(x => x.ClientReferenceCode).Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(ApplicationErrors.TopUpRequest_ClientReferenceCodeRequired)
                .MaximumLength(32).WithMessage(ApplicationErrors.TopUpRequest_ClientReferenceCodeMaxLength);
            RuleFor(x => x.BankReferenceNumber).Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(ApplicationErrors.TopUpRequest_BankReferenceNumberRequired)
                .MaximumLength(100).WithMessage(ApplicationErrors.TopUpRequest_BankReferenceNumberMaxLength);
            RuleFor(x => x.OriginalAmountRial).Cascade(CascadeMode.Stop)
                .NotNull().GreaterThan(0).WithMessage(ApplicationErrors.TopUpRequest_ConfirmedAmountMustBeGreaterThanZero);
            RuleFor(x => x.AffectiveAmountRial).Cascade(CascadeMode.Stop)
                .NotNull().GreaterThan(0).WithMessage(ApplicationErrors.TopUpRequest_ConfirmedAmountMustBeGreaterThanZero);
        });

        RuleFor(x => x.ExternalTransactionId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(ApplicationErrors.TopUpRequest_ExternalTransactionIdRequired)
            .MaximumLength(100).WithMessage(ApplicationErrors.TopUpRequest_ExternalTransactionIdMaxLength);
    }
}

