using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.CancelCourseCart;

public sealed class CancelCourseCartValidator : AbstractValidator<CancelCourseCartCommand>
{
    public CancelCourseCartValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid);
    }
}
