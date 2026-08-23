using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByeMoney.Infrastructure.Modules.Identity.Persistence.User;
using ByeMoney.Domain.Modules.Identity.Users;
public class UserRepository(ApplicationDbContext context) : BaseRepository<User, UserId>(context), IUserRepository
{
    public async Task<bool> ExistsByStrapiUserIdAsync(int strapiUserId, CancellationToken ct)
        => await Context.Set<User>().AnyAsync(u => u.StrapiUserId == strapiUserId, ct);
}