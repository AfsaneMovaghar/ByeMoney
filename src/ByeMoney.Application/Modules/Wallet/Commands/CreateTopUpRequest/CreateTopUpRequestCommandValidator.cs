using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;

public class CreateTopUpRequestCommandValidator : AbstractValidator<CreateTopUpRequestCommand>
{
    public CreateTopUpRequestCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage(ApplicationErrors.TopUpRequest_UserIdRequired);

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage(ApplicationErrors.TopUpRequest_AmountMustBeGreaterThanZero);

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

                    item.RuleFor(i => i.PriceSnapshot)
                        .GreaterThan(0)
                        .WithMessage(ApplicationErrors.TopUpRequest_PendingPriceMustBeGreaterThanZero);

                    item.RuleFor(i => i.RateSnapshot)
                        .GreaterThan(0)
                        .WithMessage(ApplicationErrors.TopUpRequest_PendingRateMustBeGreaterThanZero);
                });
        });
    }
}

