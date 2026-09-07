using ByeMoney.Application.Modules.Identity.Authorization;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByeMoney.Infrastructure.Modules.Identity.Services;

public class UserAuthorizationService(ApplicationDbContext context) : IUserAuthorizationService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<UserPermissionsDto?> GetPermissionsByStrapiUserIdAsync(int strapiUserId, CancellationToken ct = default)
    {
        var user = await _context.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.StrapiUserId == strapiUserId, ct);

        if (user is null)
            return null;

        return await GetPermissionsByUserIdAsync(user.Id, ct);
    }

    public async Task<UserPermissionsDto?> GetPermissionsByUserIdAsync(UserId userId, CancellationToken ct = default)
    {
        var userExists = await _context.Set<User>()
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId, ct);

        if (!userExists)
            return null;

        var userRoles = await _context.Set<UserRole>()
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
                .ThenInclude(r => r!.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .ToListAsync(ct);

        var roles = userRoles
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role!.Name)
            .Distinct()
            .ToList();

        var permissions = userRoles
            .Where(ur => ur.Role != null)
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Where(rp => rp.Permission != null)
            .Select(rp => rp.Permission!.Code)
            .Distinct()
            .ToList();

        return new UserPermissionsDto
        {
            UserId = userId,
            Roles = roles,
            Permissions = permissions
        };
    }
}

