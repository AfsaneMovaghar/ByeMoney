using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.AdminPurchaseCourses;

public sealed class AdminPurchaseCoursesCommandValidator : AbstractValidator<AdminPurchaseCoursesCommand>
{
    public AdminPurchaseCoursesCommandValidator()
    {
        RuleFor(x => x.BeneficiaryExternalUserId).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ApplicationErrors.User_ExternalUserIdRequired)
            .MaximumLength(100).WithMessage(ApplicationErrors.User_ExternalUserIdMaxLength);

        RuleFor(x => x.ExternalCourseIds).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(ApplicationErrors.CoursePurchase_CourseIdsListRequired)
            .NotEmpty().WithMessage(ApplicationErrors.CoursePurchase_CourseIdsListRequired)
            .Must(ids => ids.Count == ids.Distinct().Count())
            .WithMessage(ApplicationErrors.CoursePurchase_DuplicateCoursesInBasket);

        RuleForEach(x => x.ExternalCourseIds).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ApplicationErrors.CoursePurchase_ExternalCourseIdRequired)
            .MaximumLength(100).WithMessage(ApplicationErrors.CoursePurchase_ExternalCourseIdMaxLength);

        RuleFor(x => x.FreeReason).Cascade(CascadeMode.Stop)
            .MaximumLength(1000)
            .WithMessage(ApplicationErrors.CoursePurchase_FreeReasonMaxLength)
            .When(x => x.IsFree && x.FreeReason is not null);
    }
}
