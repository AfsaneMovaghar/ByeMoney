using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetCourseCart;

public sealed class GetCourseCartValidator : AbstractValidator<GetCourseCartQuery>
{
    public GetCourseCartValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid);
    }
}
