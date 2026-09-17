using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.Purchases.Interfaces;
using ByeMoney.Application.Modules.Settings;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Identity.Users;
using FluentValidation;

namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourse;

public class PurchaseCourseCommandValidator : AbstractValidator<PurchaseCourseCommand>
{
    public PurchaseCourseCommandValidator(
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

        RuleFor(x => x.ExternalCourseId)
            .NotEmpty()
            .WithMessage(ApplicationErrors.CoursePurchase_ExternalCourseIdRequired)
            .MaximumLength(100)
            .WithMessage(ApplicationErrors.CoursePurchase_ExternalCourseIdMaxLength);

        RuleFor(x => x)
            .CustomAsync(async (cmd, context, ct) =>
            {
                var buyerUserId = new UserId(cmd.BuyerUserId);

                if (await coursePurchaseRepository.ExistsByBuyerAndCourseAsync(buyerUserId, cmd.ExternalCourseId, ct))
                {
                    context.AddFailure(nameof(cmd.ExternalCourseId), ApplicationErrors.CoursePurchase_AlreadyPurchased);
                    return;
                }

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

                var course = await tarhElahiClient.GetCourseAsync(cmd.ExternalCourseId, ct);
                if (course is null)
                {
                    context.AddFailure(nameof(cmd.ExternalCourseId), ApplicationErrors.CoursePurchase_CourseNotFound);
                    return;
                }

                if (!course.Published || !course.Available)
                {
                    context.AddFailure(nameof(cmd.ExternalCourseId), ApplicationErrors.CoursePurchase_CourseNotAvailable);
                    return;
                }

                if (course.PriceRial <= 0)
                {
                    context.AddFailure(nameof(cmd.ExternalCourseId), ApplicationErrors.CoursePurchase_FreeCourseNotPurchasable);
                    return;
                }

                var conversionRate = await settingRepository.GetRialToNoorConversionRateAsync(ct);
                if (conversionRate <= 0)
                {
                    context.AddFailure("ConversionRate", ApplicationErrors.CoursePurchase_InvalidConversionRate);
                    return;
                }

                var priceInNoor = course.PriceRial / conversionRate;
                var provisioned = await walletProvisioningService.GetOrCreateUserWalletAsync(buyerUserId, ct);
                if (provisioned.Wallet.Balance < priceInNoor)
                {
                    var currentBalanceInNoor = provisioned.Wallet.Balance;
                    var shortfallInNoor = priceInNoor - currentBalanceInNoor;
                    var shortfallInRial = shortfallInNoor * conversionRate;

                    var failureState = new InsufficientBalanceFailureState(
                        currentBalanceInNoor,
                        priceInNoor,
                        shortfallInNoor,
                        shortfallInRial,
                        "INSUFFICIENT_NOOR_BALANCE");

                    var failure = new FluentValidation.Results.ValidationFailure("Wallet", string.Format(
                        ApplicationErrors.CoursePurchase_InsufficientBalance,
                        currentBalanceInNoor,
                        priceInNoor))
                    {
                        ErrorCode = "INSUFFICIENT_NOOR_BALANCE",
                        CustomState = failureState
                    };

                    context.AddFailure(failure);
                }
            });
    }
}

