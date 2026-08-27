using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Application.Modules.Identity.Users.Interface;
public interface IUserRepository : IRepository<User, UserId>
{
    Task<bool> ExistsByStrapiUserIdAsync(int strapiUserId, CancellationToken ct);
    Task<User?> GetByStrapiUserIdAsync(int strapiUserId, CancellationToken ct);
}