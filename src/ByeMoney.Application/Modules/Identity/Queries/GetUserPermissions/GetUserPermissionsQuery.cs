using MediatR;

namespace ByeMoney.Application.Modules.Identity.Queries.GetUserPermissions;

public sealed record GetUserPermissionsQuery(string ExternalUserId) : IRequest<UserPermissionsResponse?>;
