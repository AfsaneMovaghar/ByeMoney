using MediatR;

namespace ByeMoney.Application.Modules.Identity.Queries.GetUserPermissions;

public sealed record UserPermissionsResponse(
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<string> Roles,
    bool CanReviewTopUps,
    bool CanAssistTopUp);

public sealed record GetUserPermissionsQuery(string ExternalUserId) : IRequest<UserPermissionsResponse?>;
