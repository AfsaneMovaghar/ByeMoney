using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.CreateCourseCartTopUp;

public sealed class CreateCourseCartTopUpValidator : AbstractValidator<CreateCourseCartTopUpCommand>
{
    public CreateCourseCartTopUpValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid);
    }
}
