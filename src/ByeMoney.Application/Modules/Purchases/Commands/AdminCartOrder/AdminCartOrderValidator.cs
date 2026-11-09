using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Purchases;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.AdminCartOrder;

public sealed class AdminCartOrderValidator : AbstractValidator<AdminCartOrderCommand>
{
    private const int MaxItems = 100;
    private const int MaxExternalIdLength = 100;

    public AdminCartOrderValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CartId)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid);

        RuleFor(x => x.BeneficiaryExternalUserId)
            .NotEmpty()
            .WithMessage(ApplicationErrors.User_ExternalUserIdRequired)
            .MaximumLength(MaxExternalIdLength)
            .WithMessage(ApplicationErrors.User_ExternalUserIdMaxLength);

        RuleFor(x => x.Items)
            .NotNull()
            .WithMessage(CartErrors.Invalid)
            .NotEmpty()
            .WithMessage(CartErrors.Invalid)
            .Must(items => items!.Length <= MaxItems &&
                           items.All(item => item is not null) &&
                           items.GroupBy(item => (item.ExternalId, item.Type)).All(group => group.Count() == 1))
            .WithMessage(CartErrors.Invalid);

        RuleForEach(x => x.Items)
            .Must(item => item.Type is ProductItemType.Course or ProductItemType.CourseChapter)
            .WithMessage(AdminCartOrderErrors.ItemTypeNotAllowed)
            .Must(item => !string.IsNullOrWhiteSpace(item.ExternalId) &&
                          item.ExternalId == item.ExternalId.Trim() &&
                          item.ExternalId.Length <= MaxExternalIdLength &&
                          item.Quantity == 1)
            .WithMessage(CartErrors.Invalid);

        RuleFor(x => x.FreeReason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage(AdminCartOrderErrors.FreeReasonRequired)
            .Must(reason => reason!.Trim().Length <= Cart.AdminFreeReasonMaxLength)
            .WithMessage(ApplicationErrors.CoursePurchase_FreeReasonMaxLength)
            .When(x => x.IsFree);

        RuleFor(x => x.FreeReason)
            .Null()
            .WithMessage(AdminCartOrderErrors.FreeReasonNotAllowed)
            .When(x => !x.IsFree);
    }
}
