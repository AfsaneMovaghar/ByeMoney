using ByeMoney.Domain.Common._Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Identity.Users.Commands.CreateUser
{
    public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
    {
        public CreateUserCommandValidator()
        {
            RuleFor(x => x.StrapiUserId)
                .GreaterThan(0)
                .WithMessage(ValidationMessages.StrapiUserIdInvalid);

            RuleFor(x => x.DisplayName)
                .NotEmpty()
                .WithMessage(ValidationMessages.DisplayNameRequired)
                .MaximumLength(100)
                .WithMessage(ValidationMessages.DisplayNameMaxLength);

            RuleFor(x => x.Phone)
                .NotEmpty()
                .WithMessage(ValidationMessages.PhoneRequired)
                .Matches(@"^09\\d{9}$")
                .WithMessage(ValidationMessages.PhoneInvalidFormat);

            RuleFor(x => x.Role)
                .NotEmpty()
                .WithMessage(ValidationMessages.RoleRequired);

            RuleFor(x => x.UserType)
                      .IsInEnum()
                      .WithMessage(ValidationMessages.UserTypeInvalid);
        }
    }
}

