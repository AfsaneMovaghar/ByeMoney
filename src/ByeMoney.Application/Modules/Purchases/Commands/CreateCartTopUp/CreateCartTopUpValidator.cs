using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.CreateCartTopUp;

public sealed class CreateCartTopUpValidator : AbstractValidator<CreateCartTopUpCommand>
{
    public CreateCartTopUpValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid);
    }
}
