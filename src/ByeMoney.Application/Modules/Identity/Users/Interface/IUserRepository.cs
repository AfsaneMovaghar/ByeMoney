using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Application.Modules.Identity.Users.Interface;
public interface IUserRepository : IRepository<User, UserId>
{
    Task<bool> ExistsByExternalUserIdAsync(string externalUserId, CancellationToken ct);
    Task<User?> GetByExternalUserIdAsync(string externalUserId, CancellationToken ct);
}