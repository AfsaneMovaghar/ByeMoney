using MediatR;
using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Application.Modules.Identity.Users.Commands.CreateUser;

public record CreateUserCommand(
    string ExternalUserId,
    string? FirstName = null,
    string? LastName = null,
    string? Phone = null,
    string? Email = null,
    bool Confirmed = false,
    bool Blocked = false,
    UserType UserType = UserType.Normal
) : IRequest<UserId>;