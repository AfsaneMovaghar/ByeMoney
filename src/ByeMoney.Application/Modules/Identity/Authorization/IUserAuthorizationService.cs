using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Application.Modules.Identity.Authorization;

public interface IUserAuthorizationService
{
    Task<UserPermissionsDto?> GetPermissionsByStrapiUserIdAsync(int strapiUserId, CancellationToken ct = default);
    Task<UserPermissionsDto?> GetPermissionsByUserIdAsync(UserId userId, CancellationToken ct = default);
}

