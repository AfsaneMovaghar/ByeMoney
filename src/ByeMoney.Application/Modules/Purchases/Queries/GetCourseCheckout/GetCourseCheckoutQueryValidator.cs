using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetCourseCheckout;

public sealed class GetCourseCheckoutQueryValidator : AbstractValidator<GetCourseCheckoutQuery>
{
    public GetCourseCheckoutQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.ExternalCourseId).NotEmpty()
            .WithMessage(ApplicationErrors.CoursePurchase_ExternalCourseIdRequired)
            .MaximumLength(100).WithMessage(ApplicationErrors.CoursePurchase_ExternalCourseIdMaxLength);
    }
}
