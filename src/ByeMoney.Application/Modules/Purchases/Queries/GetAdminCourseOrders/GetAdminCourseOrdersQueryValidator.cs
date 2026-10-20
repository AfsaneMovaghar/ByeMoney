using ByeMoney.Application.Modules.Purchases.Orders.Validators;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminCourseOrders;

public sealed class GetAdminCourseOrdersQueryValidator : AbstractValidator<GetAdminCourseOrdersQuery>
{
    public GetAdminCourseOrdersQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(query => query.Filter).NotNull().WithMessage(PurchaseHistoryErrors.InvalidFilter)
            .DependentRules(() =>
            {
                RuleFor(query => query.Filter.ToFilter()).SetValidator(new CourseOrderFilterValidator());
                RuleFor(query => query.Filter.BuyerExternalUserId)
                    .Must(value => value is null || !string.IsNullOrWhiteSpace(value))
                    .WithMessage(PurchaseHistoryErrors.InvalidBuyer)
                    .MaximumLength(100).WithMessage(PurchaseHistoryErrors.InvalidBuyer);
            });
    }
}
