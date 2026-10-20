using ByeMoney.Application.Modules.Purchases.History.Constants;
using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.History.Validators;

public sealed class PurchaseHistoryFilterValidator : AbstractValidator<PurchaseHistoryFilter>
{
    public PurchaseHistoryFilterValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage(PurchaseHistoryErrors.InvalidPage);
        RuleFor(x => x.Page).GreaterThan(0)
            .WithMessage(PurchaseHistoryErrors.InvalidPage)
            .Must((filter, page) => filter.PageSize is >= 1 and <= 100 && page <= int.MaxValue / filter.PageSize)
            .WithMessage(PurchaseHistoryErrors.InvalidPage);
        RuleFor(x => x.BuyerExternalUserId)
            .Must(value => value is null || !string.IsNullOrWhiteSpace(value))
            .WithMessage(PurchaseHistoryErrors.InvalidBuyer)
            .MaximumLength(100).WithMessage(PurchaseHistoryErrors.InvalidBuyer);
        RuleFor(x => x.ToUtc)
            .Must((filter, end) => !end.HasValue || !filter.FromUtc.HasValue || end > filter.FromUtc)
            .WithMessage(PurchaseHistoryErrors.InvalidDateRange);
        RuleFor(x => x.FinancialStatus)
            .Must(value => value is null || value == PurchaseFinancialStatus.Paid.ToString())
            .WithMessage(PurchaseHistoryErrors.InvalidStatus);
        RuleFor(x => x.DeliveryStatus)
            .Must(value => value is null || value == PurchaseDeliveryStatus.Pending.ToString() ||
                value == PurchaseDeliveryStatus.Delivered.ToString())
            .WithMessage(PurchaseHistoryErrors.InvalidStatus);
    }
}
