using MediatR;

namespace ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;

public record SyncUserFromStrapiCommand(string ExternalUserId, bool ForceSync = false) : IRequest<Guid>;
