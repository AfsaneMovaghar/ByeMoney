using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Resources;

namespace ByeMoney.Domain.Modules.Identity.Permissions;

public class Permission : BaseEntity<PermissionId>
{
    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    private Permission() { }

    public static Permission Create(string code, string? description = null, PermissionId? id = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException(DomainErrors.Permission_CodeRequired);

        return new Permission
        {
            Id = id ?? PermissionId.New(),
            Code = code.Trim(),
            Description = description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }
}

