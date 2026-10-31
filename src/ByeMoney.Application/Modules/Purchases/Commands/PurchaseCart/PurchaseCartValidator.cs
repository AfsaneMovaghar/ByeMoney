using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCart;

public sealed class PurchaseCartValidator : AbstractValidator<PurchaseCartCommand>
{
    public PurchaseCartValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid);
    }
}
