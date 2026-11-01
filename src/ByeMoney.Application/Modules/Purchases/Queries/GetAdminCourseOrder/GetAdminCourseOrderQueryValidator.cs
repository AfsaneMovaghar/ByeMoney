using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminCourseOrder;

public sealed class GetAdminCourseOrderQueryValidator : AbstractValidator<GetAdminCourseOrderQuery>
{
    public GetAdminCourseOrderQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(query => query.OrderId).Must(CourseOrderId.IsValid).WithMessage(CourseOrderErrors.InvalidOrderId);
    }
}
