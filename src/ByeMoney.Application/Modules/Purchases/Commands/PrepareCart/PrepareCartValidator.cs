using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Domain.Modules.Purchases;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.PrepareCart;

public sealed class PrepareCartValidator : AbstractValidator<PrepareCartCommand>
{
    private const int MaxItems = 100;
    private const int MaxExternalIdLength = 100;

    public PrepareCartValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid);

        RuleFor(x => x.Items)
            .NotNull()
            .WithMessage(CartErrors.Invalid)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid)
            .Must(items => items!.Length <= MaxItems &&
                           items.GroupBy(i => (i.ExternalId, i.Type)).All(g => g.Count() == 1))
            .WithMessage(CartErrors.Invalid);

        RuleForEach(x => x.Items)
            .Must(item => item is not null &&
                          !string.IsNullOrWhiteSpace(item.ExternalId) &&
                          item.ExternalId == item.ExternalId.Trim() &&
                          item.ExternalId.Length <= MaxExternalIdLength &&
                          Enum.IsDefined(item.Type) &&
                          item.Quantity >= 1 &&
                          (item.Type == ProductItemType.Product || item.Quantity == 1))
            .WithMessage(CartErrors.Invalid);

        RuleFor(x => x.CouponCode)
            .MaximumLength(100)
            .WithMessage(CartErrors.Invalid)
            .Must(code => code is null || !string.IsNullOrWhiteSpace(code))
            .WithMessage(CartErrors.Invalid);
    }
}
