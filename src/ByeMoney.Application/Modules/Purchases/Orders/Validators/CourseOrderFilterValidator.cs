using ByeMoney.Application.Modules.Purchases.Orders.Constants;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Orders.Validators;

public sealed class CourseOrderFilterValidator : AbstractValidator<CourseOrderFilter>
{
    public CourseOrderFilterValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(filter => filter.PageSize).InclusiveBetween(1, 100).WithMessage(PurchaseHistoryErrors.InvalidPage);
        RuleFor(filter => filter.Page).GreaterThan(0).WithMessage(PurchaseHistoryErrors.InvalidPage)
            .Must((filter, page) => filter.PageSize is >= 1 and <= 100 && page <= int.MaxValue / filter.PageSize)
            .WithMessage(PurchaseHistoryErrors.InvalidPage);
        RuleFor(filter => filter.ToUtc)
            .Must((filter, end) => !end.HasValue || !filter.FromUtc.HasValue || end > filter.FromUtc)
            .WithMessage(PurchaseHistoryErrors.InvalidDateRange);
        RuleFor(filter => filter.FinancialStatus).Must(IsName<CourseOrderFinancialStatus>)
            .WithMessage(PurchaseHistoryErrors.InvalidStatus);
        RuleFor(filter => filter.DeliveryStatus).Must(IsName<CourseOrderDeliveryStatus>)
            .WithMessage(PurchaseHistoryErrors.InvalidStatus);
        RuleFor(filter => filter.SourceType).Must(IsName<CourseOrderSourceType>)
            .WithMessage(CourseOrderErrors.InvalidSourceType);
    }

    private static bool IsName<T>(string? value) where T : struct, Enum =>
        value is null || Enum.GetNames<T>().Contains(value, StringComparer.Ordinal);
}
