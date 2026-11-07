using ByeMoney.Application.Modules.Purchases.Orders.Validators;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyOrders;

public sealed class GetMyOrdersQueryValidator : AbstractValidator<GetMyOrdersQuery>
{
    public GetMyOrdersQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(query => query.Filter).NotNull().WithMessage(PurchaseHistoryErrors.InvalidFilter)
            .SetValidator(new OrderFilterValidator());
    }
}
