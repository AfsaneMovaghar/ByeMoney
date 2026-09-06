using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;

public class ConfirmTopUpCommandValidator : AbstractValidator<ConfirmTopUpCommand>
{
    public ConfirmTopUpCommandValidator()
    {
        RuleFor(x => x.TopUpRequestId.Value)
            .NotEmpty()
            .WithMessage(ApplicationErrors.TopUpRequest_IdRequired);

        RuleFor(x => x.ExternalTransactionId)
            .NotEmpty()
            .WithMessage(ApplicationErrors.TopUpRequest_ExternalTransactionIdRequired);

        RuleFor(x => x.ConfirmedAmount)
            .GreaterThan(0)
            .WithMessage(ApplicationErrors.TopUpRequest_ConfirmedAmountMustBeGreaterThanZero);
    }
}

