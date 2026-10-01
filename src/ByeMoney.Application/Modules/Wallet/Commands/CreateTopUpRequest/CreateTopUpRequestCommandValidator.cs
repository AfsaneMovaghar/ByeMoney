using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;

public class CreateTopUpRequestCommandValidator : AbstractValidator<CreateTopUpRequestCommand>
{
    public CreateTopUpRequestCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage(ApplicationErrors.TopUpRequest_UserIdRequired);

        RuleFor(x => x.Amount)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0)
            .WithMessage(ApplicationErrors.TopUpRequest_AmountMustBeGreaterThanZero)
            .Must((request, amount) => request.PaymentMethod != PaymentMethod.Gateway || decimal.Truncate(amount) == amount)
            .WithMessage(ApplicationErrors.TopUpRequest_AmountMustBeWholeNoor);

        RuleFor(x => x.PaymentMethod)
            .IsInEnum()
            .WithMessage(ApplicationErrors.TopUpRequest_PaymentMethodInvalid);

        When(x => x.PendingItems != null && x.PendingItems.Count > 0, () =>
        {
            RuleForEach(x => x.PendingItems)
                .ChildRules(item =>
                {
                    item.RuleFor(i => i.ExternalId)
                        .NotEmpty()
                        .WithMessage(ApplicationErrors.TopUpRequest_PendingItemExternalIdRequired)
                        .MaximumLength(100)
                        .WithMessage(ApplicationErrors.CoursePurchase_ExternalCourseIdMaxLength);

                    item.RuleFor(i => i.PriceNoorSnapshot)
                        .GreaterThan(0)
                        .WithMessage(ApplicationErrors.TopUpRequest_PendingPriceMustBeGreaterThanZero)
                        .Must(price => decimal.Truncate(price) == price)
                        .WithMessage(ApplicationErrors.TopUpRequest_AmountMustBeWholeNoor);
                });
        });
    }
}

