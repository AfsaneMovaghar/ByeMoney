
using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Domain.Common._Resources;
using FluentValidation;

namespace ByeMoney.Application.Modules.Identity.Users.Commands.CreateUser;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    private readonly IUserRepository _userRepository;

    public CreateUserCommandValidator(IUserRepository userRepository)
    {
        _userRepository = userRepository;

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
            .Matches(@"^09\d{9}$")
            .WithMessage(ValidationMessages.PhoneInvalidFormat);

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage(ValidationMessages.RoleRequired);

        RuleFor(x => x.UserType)
            .IsInEnum()
            .WithMessage(ValidationMessages.UserTypeInvalid);

        RuleFor(x => x.StrapiUserId)
            .MustAsync(BeUniqueStrapiId)
            .WithMessage(ValidationMessages.StrapiIdAlreadyExists)
            .WhenAsync((command, ct) => Task.FromResult(command.StrapiUserId > 0));
    }

    private async Task<bool> BeUniqueStrapiId(int strapiUserId, CancellationToken ct)
        => !await _userRepository.ExistsByStrapiUserIdAsync(strapiUserId, ct);
}