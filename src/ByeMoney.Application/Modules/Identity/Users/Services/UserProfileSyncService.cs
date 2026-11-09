using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Application.Modules.TarhElahiIntegration.Exceptions;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Domain.Modules.Identity.Users;
using Microsoft.Extensions.Logging;

namespace ByeMoney.Application.Modules.Identity.Users.Services;

/// <summary>
/// ساخت یا تازه‌سازی کاربر محلی از روی کاربر استرپی؛ مشترک بین فرمان همگام‌سازی و مسیرهای ادمین،
/// تا هیچ هندلری برای همگام‌سازی فرمان MediatR تودرتو نفرستد.
/// </summary>
public sealed class UserProfileSyncService(
    IUserProvisioningService provisioningService,
    ITarhElahiIntegrationClient tarhElahiClient,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ILogger<UserProfileSyncService> logger)
{
    public async Task<Guid> SyncAsync(string externalUserId, bool forceSync, CancellationToken ct)
    {
        var result = await provisioningService.EnsureUserFromExternalAsync(externalUserId, ct);

        if (result.Created)
        {
            return result.User.Id.Value;
        }

        return await UpdateUserIfNeededAsync(result.User, externalUserId, forceSync, ct);
    }

    private async Task<Guid> UpdateUserIfNeededAsync(User user, string externalUserId, bool forceSync, CancellationToken ct)
    {
        if (!forceSync && !user.NeedsProfileSync())
        {
            return user.Id.Value;
        }

        try
        {
            var strapiUser = await tarhElahiClient.GetUserAsync(externalUserId, ct);

            if (strapiUser is not null)
            {
                user.SyncProfile(
                    phone: strapiUser.PhoneNumber,
                    email: strapiUser.Email,
                    firstName: strapiUser.FirstName,
                    lastName: strapiUser.LastName,
                    confirmed: strapiUser.Confirmed,
                    blocked: strapiUser.Blocked
                );

                userRepository.Update(user);
                await unitOfWork.SaveChangesAsync(ct);

                logger.LogInformation("Successfully refreshed profile for user '{ExternalUserId}'.", externalUserId);
            }
            else
            {
                logger.LogWarning("TarhElahi user '{ExternalUserId}' not found during refresh; proceeding with stale profile data.", externalUserId);
            }
        }
        catch (TarhElahiUnavailableException)
        {
            logger.LogWarning("TarhElahi unavailable while refreshing profile for user '{ExternalUserId}'; proceeding with stale profile data.", externalUserId);
        }

        return user.Id.Value;
    }
}
