using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.CancelGatewayTopUp;

public sealed class CancelGatewayTopUpCommandValidator : AbstractValidator<CancelGatewayTopUpCommand>
{
    public CancelGatewayTopUpCommandValidator()
    {
        RuleFor(x => x.ClientReferenceCode).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ApplicationErrors.TopUpRequest_ClientReferenceCodeRequired)
            .MaximumLength(32).WithMessage(ApplicationErrors.TopUpRequest_ClientReferenceCodeMaxLength);
    }
}
