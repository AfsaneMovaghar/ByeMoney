using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;

public class CreateTopUpRequestCommandValidator : AbstractValidator<CreateTopUpRequestCommand>
{
    public CreateTopUpRequestCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage(ApplicationErrors.TopUpRequest_UserIdRequired);

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage(ApplicationErrors.TopUpRequest_AmountMustBeGreaterThanZero);

        RuleFor(x => x.PaymentMethod)
            .IsInEnum()
            .WithMessage(ApplicationErrors.TopUpRequest_PaymentMethodInvalid);
    }
}

