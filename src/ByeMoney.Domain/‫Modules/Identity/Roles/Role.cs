using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Modules.Identity.Roles;

public class Role : BaseEntity<RoleId>
{
    public string Name { get; private set; } = string.Empty;

    private readonly List<RolePermission> _rolePermissions = [];
    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();

    private Role() { }

    public static Role Create(string name, RoleId? id = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException(DomainErrors.Role_NameRequired);

        return new Role
        {
            Id = id ?? RoleId.New(),
            Name = name.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }
}

