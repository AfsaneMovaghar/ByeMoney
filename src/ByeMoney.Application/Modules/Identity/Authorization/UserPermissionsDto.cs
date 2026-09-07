using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Application.Modules.Identity.Authorization;

public class UserPermissionsDto
{
    public UserId UserId { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = [];
    public IReadOnlyCollection<string> Permissions { get; set; } = [];
}

