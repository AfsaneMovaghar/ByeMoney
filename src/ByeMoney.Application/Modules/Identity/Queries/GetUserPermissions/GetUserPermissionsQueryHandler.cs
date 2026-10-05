using ByeMoney.Application.Modules.Identity.Authorization;
using ByeMoney.Domain.Modules.Identity.Permissions;
using MediatR;

namespace ByeMoney.Application.Modules.Identity.Queries.GetUserPermissions;

public sealed class GetUserPermissionsQueryHandler(IUserAuthorizationService authorizationService)
    : IRequestHandler<GetUserPermissionsQuery, UserPermissionsResponse?>
{
    public async Task<UserPermissionsResponse?> Handle(
        GetUserPermissionsQuery request, CancellationToken cancellationToken)
    {
        var permissions = await authorizationService.GetPermissionsByExternalUserIdAsync(
            request.ExternalUserId, cancellationToken);

        if (permissions is null)
            return null;

        var canReview = permissions.Permissions.Contains(Permissions.TopUp.Review);
        return new UserPermissionsResponse(
            permissions.Permissions, permissions.Roles, canReview, canReview);
    }
}
