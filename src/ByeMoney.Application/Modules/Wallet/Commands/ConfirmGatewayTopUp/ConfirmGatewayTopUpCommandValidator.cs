using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;

public sealed class ConfirmGatewayTopUpCommandValidator : AbstractValidator<ConfirmGatewayTopUpCommand>
{
    public ConfirmGatewayTopUpCommandValidator()
    {
        RuleFor(x => x.ClientReferenceCode).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ApplicationErrors.TopUpRequest_ClientReferenceCodeRequired)
            .MaximumLength(32).WithMessage(ApplicationErrors.TopUpRequest_ClientReferenceCodeMaxLength);

        RuleFor(x => x.Gateway).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ApplicationErrors.TopUpRequest_GatewayRequired)
            .MaximumLength(30).WithMessage(ApplicationErrors.TopUpRequest_GatewayMaxLength);

        RuleFor(x => x.ExternalTransactionId).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ApplicationErrors.TopUpRequest_ExternalTransactionIdRequired)
            .MaximumLength(100).WithMessage(ApplicationErrors.TopUpRequest_ExternalTransactionIdMaxLength);

        RuleFor(x => x.BankReferenceNumber).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ApplicationErrors.TopUpRequest_BankReferenceNumberRequired)
            .MaximumLength(100).WithMessage(ApplicationErrors.TopUpRequest_BankReferenceNumberMaxLength);

        RuleFor(x => x.OriginalAmountRial).Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage(ApplicationErrors.TopUpRequest_AmountRialMustBeGreaterThanZero);

        RuleFor(x => x.AffectiveAmountRial).Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage(ApplicationErrors.TopUpRequest_AmountRialMustBeGreaterThanZero);
    }
}

