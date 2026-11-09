using ByeMoney.Application.Modules.Identity.Users.Services;
using MediatR;

namespace ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;

public class SyncUserFromStrapiCommandHandler(UserProfileSyncService syncService)
    : IRequestHandler<SyncUserFromStrapiCommand, Guid>
{
    public Task<Guid> Handle(SyncUserFromStrapiCommand request, CancellationToken ct) =>
        syncService.SyncAsync(request.ExternalUserId, request.ForceSync, ct);
}
