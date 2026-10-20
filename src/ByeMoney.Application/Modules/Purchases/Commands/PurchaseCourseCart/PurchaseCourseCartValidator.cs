using ByeMoney.Application.Modules.Purchases.Cart.Constants;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourseCart;

public sealed class PurchaseCourseCartValidator : AbstractValidator<PurchaseCourseCartCommand>
{
    public PurchaseCourseCartValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid);
    }
}
