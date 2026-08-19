using FluentValidation;

namespace ByeMoney.Application.Modules.Identity.Users.Commands.CreateUser;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.StrapiUserId)
            .GreaterThan(0);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Phone)
            .NotEmpty()
            .Matches(@"^09\d{9}$")
            .WithMessage("شماره موبایل معتبر نیست.");
        
        RuleFor(x => x.Role).NotEmpty();
        
        RuleFor(x => x.UserType)
                  .IsInEnum();
    }
}