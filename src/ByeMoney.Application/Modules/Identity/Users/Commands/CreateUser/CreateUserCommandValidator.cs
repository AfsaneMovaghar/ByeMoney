
using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Resources;
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
            .WithMessage(ApplicationErrors.User_StrapiUserIdInvalid);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage(ApplicationErrors.User_DisplayNameRequired)
            .MaximumLength(100)
            .WithMessage(ApplicationErrors.User_DisplayNameMaxLength);

        RuleFor(x => x.Phone)
            .NotEmpty()
            .WithMessage(ApplicationErrors.User_PhoneRequired)
            .Matches(@"^09\d{9}$")
            .WithMessage(ApplicationErrors.User_PhoneInvalidFormat);

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage(ApplicationErrors.User_RoleRequired);

        RuleFor(x => x.UserType)
            .IsInEnum()
            .WithMessage(ApplicationErrors.User_UserTypeInvalid);

        RuleFor(x => x.StrapiUserId)
            .MustAsync(BeUniqueStrapiId)
            .WithMessage(ApplicationErrors.User_StrapiIdAlreadyExists)
            .WhenAsync((command, ct) => Task.FromResult(command.StrapiUserId > 0));
    }

    private async Task<bool> BeUniqueStrapiId(int strapiUserId, CancellationToken ct)
        => !await _userRepository.ExistsByStrapiUserIdAsync(strapiUserId, ct);
}