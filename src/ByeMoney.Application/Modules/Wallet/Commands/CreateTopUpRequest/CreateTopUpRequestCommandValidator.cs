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

        When(x => x.PendingItemType.HasValue, () =>
        {
            RuleFor(x => x.PendingItemExternalId)
                .NotEmpty()
                .WithMessage(ApplicationErrors.TopUpRequest_PendingItemExternalIdRequired)
                .MaximumLength(100)
                .WithMessage(ApplicationErrors.CoursePurchase_ExternalCourseIdMaxLength);

            RuleFor(x => x.PendingPriceSnapshot)
                .NotNull()
                .WithMessage(ApplicationErrors.TopUpRequest_PendingPriceMustBeGreaterThanZero)
                .GreaterThan(0)
                .WithMessage(ApplicationErrors.TopUpRequest_PendingPriceMustBeGreaterThanZero);

            RuleFor(x => x.PendingRateSnapshot)
                .NotNull()
                .WithMessage(ApplicationErrors.TopUpRequest_PendingRateMustBeGreaterThanZero)
                .GreaterThan(0)
                .WithMessage(ApplicationErrors.TopUpRequest_PendingRateMustBeGreaterThanZero);
        });
    }
}

