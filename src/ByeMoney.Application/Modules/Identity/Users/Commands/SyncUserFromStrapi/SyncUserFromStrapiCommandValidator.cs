using ByeMoney.Domain.Common._Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;

public class SyncUserFromStrapiCommandValidator : AbstractValidator<SyncUserFromStrapiCommand>
{
    public SyncUserFromStrapiCommandValidator()
    {
        RuleFor(x => x.StrapiUserId)
            .GreaterThan(0)
            .WithMessage(ValidationMessages.StrapiUserIdInvalid);
    }
}
