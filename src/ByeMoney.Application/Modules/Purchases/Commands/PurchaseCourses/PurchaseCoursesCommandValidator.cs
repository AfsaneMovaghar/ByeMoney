using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourses;

public class PurchaseCoursesCommandValidator : AbstractValidator<PurchaseCoursesCommand>
{
    public PurchaseCoursesCommandValidator(
        ICoursePurchaseRepository coursePurchaseRepository,
        IUserRepository userRepository,
        ITarhElahiIntegrationClient tarhElahiClient,
        ISystemSettingRepository settingRepository,
        IUserWalletProvisioningService walletProvisioningService)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.BuyerUserId)
            .NotEmpty()
            .WithMessage(ApplicationErrors.CoursePurchase_BuyerUserIdRequired);

        RuleFor(x => x.ExternalCourseIds)
            .NotNull()
            .WithMessage(ApplicationErrors.CoursePurchase_CourseIdsListRequired)
            .NotEmpty()
            .WithMessage(ApplicationErrors.CoursePurchase_CourseIdsListRequired)
            .Must(ids => ids == null || ids.Count == ids.Distinct().Count())
            .WithMessage(ApplicationErrors.CoursePurchase_DuplicateCoursesInBasket);

        RuleForEach(x => x.ExternalCourseIds)
            .NotEmpty()
            .WithMessage(ApplicationErrors.CoursePurchase_ExternalCourseIdRequired)
            .MaximumLength(100)
            .WithMessage(ApplicationErrors.CoursePurchase_ExternalCourseIdMaxLength);

        RuleFor(x => x)
            .CustomAsync(async (cmd, context, ct) =>
            {
                if (cmd.ExternalCourseIds == null || cmd.ExternalCourseIds.Count == 0)
                {
                    return;
                }

                var buyerUserId = new UserId(cmd.BuyerUserId);

                var user = await userRepository.GetByIdAsync(buyerUserId, ct);
                if (user is null)
                {
                    context.AddFailure(nameof(cmd.BuyerUserId), ApplicationErrors.CoursePurchase_UserNotFound);
                    return;
                }

                if (!user.IsActive)
                {
                    context.AddFailure(nameof(cmd.BuyerUserId), ApplicationErrors.CoursePurchase_UserNotActive);
                    return;
                }

                var conversionRate = await settingRepository.GetRialToNoorConversionRateAsync(ct);
                if (conversionRate <= 0)
                {
                    context.AddFailure("ConversionRate", ApplicationErrors.CoursePurchase_InvalidConversionRate);
                    return;
                }

                var validCourses = new List<(TarhElahiCourseDto Course, decimal PriceInNoor)>();
                var hasCourseErrors = false;

                foreach (var courseId in cmd.ExternalCourseIds)
                {
                    if (string.IsNullOrWhiteSpace(courseId))
                    {
                        hasCourseErrors = true;
                        continue;
                    }

                    if (await coursePurchaseRepository.ExistsByBuyerAndCourseAsync(buyerUserId, courseId, ct))
                    {
                        context.AddFailure($"ExternalCourseIds[{courseId}]", ApplicationErrors.CoursePurchase_AlreadyPurchased);
                        hasCourseErrors = true;
                        continue;
                    }

                    var course = await tarhElahiClient.GetCourseAsync(courseId, ct);
                    if (course is null)
                    {
                        context.AddFailure($"ExternalCourseIds[{courseId}]", ApplicationErrors.CoursePurchase_CourseNotFound);
                        hasCourseErrors = true;
                        continue;
                    }

                    if (!course.Published || !course.Available)
                    {
                        context.AddFailure($"ExternalCourseIds[{courseId}]", ApplicationErrors.CoursePurchase_CourseNotAvailable);
                        hasCourseErrors = true;
                        continue;
                    }

                    if (course.PriceRial <= 0)
                    {
                        context.AddFailure($"ExternalCourseIds[{courseId}]", ApplicationErrors.CoursePurchase_FreeCourseNotPurchasable);
                        hasCourseErrors = true;
                        continue;
                    }

                    var priceInNoor = course.PriceRial / conversionRate;
                    validCourses.Add((course, priceInNoor));
                }

                // All-or-nothing: if any course failed validation, do not proceed to balance check
                if (hasCourseErrors)
                {
                    return;
                }

                var totalPriceInNoor = validCourses.Sum(x => x.PriceInNoor);
                var provisioned = await walletProvisioningService.GetOrCreateUserWalletAsync(buyerUserId, ct);

                if (provisioned.Wallet.Balance < totalPriceInNoor)
                {
                    var currentBalanceInNoor = provisioned.Wallet.Balance;
                    var shortfallInNoor = totalPriceInNoor - currentBalanceInNoor;
                    var shortfallInRial = shortfallInNoor * conversionRate;

                    var pendingSnapshots = validCourses.Select(c => new PendingItemSnapshot(
                        PendingItemType.Course,
                        c.Course.ExternalId,
                        c.Course.PriceRial,
                        conversionRate)).ToList();

                    var failureState = new InsufficientBalanceFailureState(
                        currentBalanceInNoor,
                        totalPriceInNoor,
                        shortfallInNoor,
                        shortfallInRial,
                        "INSUFFICIENT_NOOR_BALANCE",
                        pendingSnapshots);

                    var failure = new FluentValidation.Results.ValidationFailure("Wallet", string.Format(
                        ApplicationErrors.CoursePurchase_InsufficientBalanceBasket,
                        currentBalanceInNoor,
                        totalPriceInNoor))
                    {
                        ErrorCode = "INSUFFICIENT_NOOR_BALANCE",
                        CustomState = failureState
                    };

                    context.AddFailure(failure);
                }
            });
    }
}

