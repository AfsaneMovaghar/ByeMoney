using ByeMoney.Domain.Modules.Identity.Permissions;

namespace ByeMoney.Domain.Modules.Identity.Roles;

public class RolePermission
{
    public RoleId RoleId { get; private set; }
    public Role? Role { get; private set; }

    public PermissionId PermissionId { get; private set; }
    public Permission? Permission { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private RolePermission() { }

    public static RolePermission Create(RoleId roleId, PermissionId permissionId)
    {
        return new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId,
            CreatedAt = DateTime.UtcNow
        };
    }
}

