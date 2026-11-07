using ByeMoney.Application.Modules.Purchases.Settlements.Validators;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetOrderSettlementPeriods;

public sealed class GetOrderSettlementPeriodsQueryValidator : AbstractValidator<GetOrderSettlementPeriodsQuery>
{
    public GetOrderSettlementPeriodsQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        Include(new SettlementPagingValidator());
    }
}
