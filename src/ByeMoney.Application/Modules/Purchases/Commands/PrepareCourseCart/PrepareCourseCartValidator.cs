using ByeMoney.Application.Modules.Purchases.Cart.Constants;
using ByeMoney.Domain.Modules.Purchases;
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

        RuleFor(x => x)
            .Must(x => (x.Items != null && x.Items.Length > 0) || (x.ExternalCourseIds != null && x.ExternalCourseIds.Length > 0))
            .WithMessage(CartErrors.Invalid);

        When(x => x.Items != null && x.Items.Length > 0, () =>
        {
            RuleFor(x => x.Items!)
                .Must(items => items.Length <= 100 &&
                               items.GroupBy(i => (i.ExternalId, i.Type)).All(g => g.Count() == 1))
                .WithMessage(CartErrors.Invalid);

            RuleForEach(x => x.Items!)
                .Must(item => !string.IsNullOrWhiteSpace(item.ExternalId) &&
                              item.ExternalId == item.ExternalId.Trim() &&
                              item.ExternalId.Length <= 100 &&
                              item.Quantity >= 1 &&
                              (item.Type != ProductItemType.Course || item.Quantity == 1) &&
                              (item.Type != ProductItemType.CourseChapter || item.Quantity == 1))
                .WithMessage(CartErrors.Invalid);
        });

        When(x => x.Items == null || x.Items.Length == 0, () =>
        {
            RuleFor(x => x.ExternalCourseIds)
                .NotNull()
                .WithMessage(CartErrors.Invalid)
                .NotEmpty()
                .WithMessage(CartErrors.Invalid)
                .Must(ids => ids != null && ids.Length <= 100 && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length)
                .WithMessage(CartErrors.Invalid);

            RuleForEach(x => x.ExternalCourseIds!)
                .NotEmpty()
                .WithMessage(CartErrors.Invalid)
                .MaximumLength(100)
                .WithMessage(CartErrors.Invalid)
                .Must(id => id == id.Trim())
                .WithMessage(CartErrors.Invalid);
        });

        RuleFor(x => x.CouponCode)
            .MaximumLength(100)
            .WithMessage(CartErrors.Invalid)
            .Must(code => code is null || !string.IsNullOrWhiteSpace(code))
            .WithMessage(CartErrors.Invalid);
    }
}
