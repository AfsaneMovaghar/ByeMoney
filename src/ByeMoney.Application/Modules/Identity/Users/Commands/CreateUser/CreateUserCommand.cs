using MediatR;
using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Application.Modules.Identity.Users.Commands.CreateUser;

public record CreateUserCommand(
    int StrapiUserId,
    string DisplayName,
    string Phone,
   string Role,
   UserType UserType
) : IRequest<UserId>;