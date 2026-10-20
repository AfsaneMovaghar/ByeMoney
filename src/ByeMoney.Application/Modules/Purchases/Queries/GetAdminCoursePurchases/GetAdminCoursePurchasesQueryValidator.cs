using ByeMoney.Application.Modules.Purchases.History.Validators;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminCoursePurchases;

public sealed class GetAdminCoursePurchasesQueryValidator : AbstractValidator<GetAdminCoursePurchasesQuery>
{
    public GetAdminCoursePurchasesQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.Filter).NotNull().WithMessage(PurchaseHistoryErrors.InvalidFilter)
            .SetValidator(new PurchaseHistoryFilterValidator());
    }
}
