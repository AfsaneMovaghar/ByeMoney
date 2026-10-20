using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.History.Validators;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyCoursePurchases;

public sealed class GetMyCoursePurchasesQueryValidator : AbstractValidator<GetMyCoursePurchasesQuery>
{
    public GetMyCoursePurchasesQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => new PurchaseHistoryFilter(x.Page, x.PageSize))
            .SetValidator(new PurchaseHistoryFilterValidator());
    }
}
