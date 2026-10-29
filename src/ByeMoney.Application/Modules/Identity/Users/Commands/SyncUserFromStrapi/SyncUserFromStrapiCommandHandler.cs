using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Application.Modules.TarhElahiIntegration.Exceptions;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;

public class SyncUserFromStrapiCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ITarhElahiIntegrationClient tarhElahiClient,
    ILogger<SyncUserFromStrapiCommandHandler> logger,
    IWelcomeGiftService welcomeGiftService) : IRequestHandler<SyncUserFromStrapiCommand, Guid>
{
    public async Task<Guid> Handle(SyncUserFromStrapiCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByExternalUserIdAsync(request.ExternalUserId, ct);

        if (user is null)
        {
            return await CreateUserFromTarhElahiAsync(request.ExternalUserId, ct);
        }

        return await UpdateUserIfNeededAsync(user, request.ExternalUserId, request.ForceSync, ct);
    }

    private async Task<Guid> CreateUserFromTarhElahiAsync(string externalUserId, CancellationToken ct)
    {
        var strapiUser = await tarhElahiClient.GetUserAsync(externalUserId, ct);

        if (strapiUser is null)
        {
            throw new NotFoundException(string.Format(ApplicationErrors.User_NotFoundInTarhElahi, externalUserId));
        }

        var newUser = User.CreateFromStrapi(
            externalUserId: strapiUser.ExternalUserId,
            phone: strapiUser.PhoneNumber,
            email: strapiUser.Email,
            firstName: strapiUser.FirstName,
            lastName: strapiUser.LastName,
            confirmed: strapiUser.Confirmed,
            blocked: strapiUser.Blocked
        );

        await userRepository.AddAsync(newUser, ct);
        await welcomeGiftService.GrantWelcomeGiftIfEligibleAsync(newUser, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Created new internal user for TarhElahi user '{ExternalUserId}'.", externalUserId);
        return newUser.Id.Value;
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
        catch (TarhElahiUnavailableException ex)
        {
            logger.LogWarning(ex, "TarhElahi unavailable while refreshing profile for user '{ExternalUserId}'; proceeding with stale profile data.", externalUserId);
        }

        return user.Id.Value;
    }
}

