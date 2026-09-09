
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

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.ExternalUserId)
            .NotEmpty()
            .WithMessage(ApplicationErrors.User_ExternalUserIdRequired)
            .MaximumLength(100)
            .WithMessage(ApplicationErrors.User_ExternalUserIdMaxLength)
            .MustAsync(BeUniqueExternalUserId)
            .WithMessage(ApplicationErrors.User_ExternalUserIdAlreadyExists);

        RuleFor(x => x.FirstName)
            .MaximumLength(100)
            .WithMessage(ApplicationErrors.User_FirstNameMaxLength)
            .When(x => !string.IsNullOrEmpty(x.FirstName));

        RuleFor(x => x.LastName)
            .MaximumLength(100)
            .WithMessage(ApplicationErrors.User_LastNameMaxLength)
            .When(x => !string.IsNullOrEmpty(x.LastName));

        RuleFor(x => x.Phone)
            .Matches(@"^09\d{9}$")
            .WithMessage(ApplicationErrors.User_PhoneInvalidFormat)

            .MustAsync(BeUniquePhone)
            .WithMessage(ApplicationErrors.User_PhoneAlreadyExists)
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.Email)
            .EmailAddress()
            .WithMessage(ApplicationErrors.User_EmailInvalidFormat)
            .MaximumLength(255)
            .WithMessage(ApplicationErrors.User_EmailMaxLength)
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.UserType)
            .IsInEnum()
            .WithMessage(ApplicationErrors.User_UserTypeInvalid);
    }

    private async Task<bool> BeUniqueExternalUserId(string externalUserId, CancellationToken ct)
        => !await _userRepository.ExistsByExternalUserIdAsync(externalUserId, ct);

    private async Task<bool> BeUniquePhone(string? phone, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(phone))
            return true;

        return !await _userRepository.ExistsByPhoneAsync(phone, ct);
    }
}