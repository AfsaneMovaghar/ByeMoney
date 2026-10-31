using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetCart;

public sealed class GetCartValidator : AbstractValidator<GetCartQuery>
{
    public GetCartValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid);
    }
}
