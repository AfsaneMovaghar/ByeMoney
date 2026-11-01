using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminCourseOrderReceipt;

public sealed class GetAdminCourseOrderReceiptQueryValidator : AbstractValidator<GetAdminCourseOrderReceiptQuery>
{
    public GetAdminCourseOrderReceiptQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(query => query.OrderId).Must(CourseOrderId.IsValid).WithMessage(CourseOrderErrors.InvalidOrderId);
    }
}
