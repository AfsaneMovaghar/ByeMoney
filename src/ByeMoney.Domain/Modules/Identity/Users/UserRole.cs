using ByeMoney.Domain.Modules.Identity.Roles;

namespace ByeMoney.Domain.Modules.Identity.Users;

public class UserRole
{
    public UserId UserId { get; private set; }
    public User? User { get; private set; }

    public RoleId RoleId { get; private set; }
    public Role? Role { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private UserRole() { }

    public static UserRole Create(UserId userId, RoleId roleId)
    {
        return new UserRole
        {
            UserId = userId,
            RoleId = roleId,
            CreatedAt = DateTime.UtcNow
        };
    }
}

