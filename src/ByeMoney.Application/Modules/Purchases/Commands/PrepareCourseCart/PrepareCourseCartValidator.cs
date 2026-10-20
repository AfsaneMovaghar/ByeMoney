using ByeMoney.Application.Modules.Purchases.Cart.Constants;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.PrepareCourseCart;

public sealed class PrepareCourseCartValidator : AbstractValidator<PrepareCourseCartCommand>
{
    public PrepareCourseCartValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid);

        RuleFor(x => x.ExternalCourseIds)
            .NotNull()
            .WithMessage(CartErrors.Invalid)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid)
            .Must(ids => ids.Length <= 100 && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length)
            .WithMessage(CartErrors.Invalid);

        RuleForEach(x => x.ExternalCourseIds)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid)
            .MaximumLength(100)
            .WithMessage(CartErrors.Invalid)
            .Must(id => id == id.Trim())
            .WithMessage(CartErrors.Invalid);

        RuleFor(x => x.CouponCode)
            .MaximumLength(100)
            .WithMessage(CartErrors.Invalid)
            .Must(code => code is null || !string.IsNullOrWhiteSpace(code))
            .WithMessage(CartErrors.Invalid);
    }
}
