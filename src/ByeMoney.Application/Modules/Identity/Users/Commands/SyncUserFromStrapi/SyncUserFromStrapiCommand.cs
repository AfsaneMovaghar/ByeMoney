using MediatR;

namespace ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;

public record SyncUserFromStrapiCommand(string ExternalUserId) : IRequest<Guid>;
