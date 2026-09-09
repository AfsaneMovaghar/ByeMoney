using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Application.Modules.Identity.Authorization;

public interface IUserAuthorizationService
{
    Task<UserPermissionsDto?> GetPermissionsByExternalUserIdAsync(string externalUserId, CancellationToken ct = default);
    Task<UserPermissionsDto?> GetPermissionsByUserIdAsync(UserId userId, CancellationToken ct = default);
}

