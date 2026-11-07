using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Settlements.Validators;

public interface ISettlementPagedQuery
{
    int Page { get; }
    int PageSize { get; }
}

public sealed class SettlementPagingValidator : AbstractValidator<ISettlementPagedQuery>
{
    public const int MaxPageSize = 100;

    public SettlementPagingValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, MaxPageSize)
            .WithMessage(PurchaseHistoryErrors.InvalidPage);

        RuleFor(query => query.Page)
            .GreaterThan(0)
            .WithMessage(PurchaseHistoryErrors.InvalidPage)
            .Must((query, page) => page <= int.MaxValue / query.PageSize)
            .WithMessage(PurchaseHistoryErrors.InvalidPage);
    }
}
