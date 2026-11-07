using ByeMoney.Application.Modules.Purchases.Settlements.Validators;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetOrderSettlementPreview;

public sealed class GetOrderSettlementPreviewQueryValidator : AbstractValidator<GetOrderSettlementPreviewQuery>
{
    public GetOrderSettlementPreviewQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        Include(new SettlementPagingValidator());
    }
}
