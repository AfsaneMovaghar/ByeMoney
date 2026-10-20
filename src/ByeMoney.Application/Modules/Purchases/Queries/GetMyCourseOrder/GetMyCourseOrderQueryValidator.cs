using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyCourseOrder;

public sealed class GetMyCourseOrderQueryValidator : AbstractValidator<GetMyCourseOrderQuery>
{
    public GetMyCourseOrderQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(query => query.OrderId).Must(CourseOrderId.IsValid).WithMessage(CourseOrderErrors.InvalidOrderId);
    }
}
