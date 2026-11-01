using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyCourseOrderReceipt;

public sealed class GetMyCourseOrderReceiptQueryValidator : AbstractValidator<GetMyCourseOrderReceiptQuery>
{
    public GetMyCourseOrderReceiptQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(query => query.OrderId).Must(CourseOrderId.IsValid).WithMessage(CourseOrderErrors.InvalidOrderId);
    }
}
