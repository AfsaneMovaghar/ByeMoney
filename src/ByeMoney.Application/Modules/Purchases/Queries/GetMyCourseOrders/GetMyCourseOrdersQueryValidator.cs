using ByeMoney.Application.Modules.Purchases.Orders.Validators;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyCourseOrders;

public sealed class GetMyCourseOrdersQueryValidator : AbstractValidator<GetMyCourseOrdersQuery>
{
    public GetMyCourseOrdersQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(query => query.Filter).NotNull().WithMessage(PurchaseHistoryErrors.InvalidFilter)
            .SetValidator(new CourseOrderFilterValidator());
    }
}
