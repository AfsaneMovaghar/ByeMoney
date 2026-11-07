using ByeMoney.Application.Modules.Purchases.Settlements.Validators;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetOrderSettlementPeriodOrders;

public sealed class GetOrderSettlementPeriodOrdersQueryValidator : AbstractValidator<GetOrderSettlementPeriodOrdersQuery>
{
    public GetOrderSettlementPeriodOrdersQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(query => query.PeriodId)
            .NotEmpty()
            .WithMessage(OrderSettlementErrors.InvalidPeriodId);

        Include(new SettlementPagingValidator());
    }
}
