using Microsoft.AspNetCore.Authorization;

namespace ByeMoney.API.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

