using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Wallet.Commands.AdminGrantGiftNoor;

public class AdminGrantGiftNoorCommandValidator : AbstractValidator<AdminGrantGiftNoorCommand>
{
    public AdminGrantGiftNoorCommandValidator(GiftSettings? settings = null)
    {
        var config = settings ?? new GiftSettings();

        RuleFor(x => x.IdempotencyKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ApplicationErrors.Gift_IdempotencyKeyRequired)
            .MaximumLength(200);

        RuleFor(x => x.Reason)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ApplicationErrors.Gift_ReasonRequired)
            .MaximumLength(500);

        RuleFor(x => x.AmountNoor)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage(ApplicationErrors.Gift_AmountMustBePositiveInteger)
            .Must(amount => amount == decimal.Floor(amount)).WithMessage(ApplicationErrors.Gift_AmountMustBePositiveInteger);

        RuleFor(x => x.ExternalUserIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(ApplicationErrors.Gift_UsersRequired)
            .NotEmpty().WithMessage(ApplicationErrors.Gift_UsersRequired)
            .Must(users => users.Count <= config.MaxUsersPerBatch)
            .WithMessage(string.Format(ApplicationErrors.Gift_UsersExceedsLimit, config.MaxUsersPerBatch))
            .Must(users => users.Distinct(StringComparer.Ordinal).Count() == users.Count)
            .WithMessage(ApplicationErrors.Gift_DuplicateUsersNotAllowed);

        RuleForEach(x => x.ExternalUserIds)
            .NotEmpty().WithMessage(ApplicationErrors.Gift_UsersRequired);
    }
}

