using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interface;
using ByeMoney.Application.Modules.TarhElahiIntegration.Exceptions;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;

public class SyncUserFromStrapiCommandHandler : IRequestHandler<SyncUserFromStrapiCommand, Guid>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITarhElahiIntegrationClient _tarhElahiClient;
    private readonly ILogger<SyncUserFromStrapiCommandHandler> _logger;

    public SyncUserFromStrapiCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ITarhElahiIntegrationClient tarhElahiClient,
        ILogger<SyncUserFromStrapiCommandHandler> logger)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _tarhElahiClient = tarhElahiClient;
        _logger = logger;
    }

    public async Task<Guid> Handle(SyncUserFromStrapiCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByExternalUserIdAsync(request.ExternalUserId, ct);

        if (user is null)
        {
            return await CreateUserFromTarhElahiAsync(request.ExternalUserId, ct);
        }

        return await UpdateUserIfNeededAsync(user, request.ExternalUserId, ct);
    }

    private async Task<Guid> CreateUserFromTarhElahiAsync(string externalUserId, CancellationToken ct)
    {
        var strapiUser = await _tarhElahiClient.GetUserAsync(externalUserId, ct);

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

        await _userRepository.AddAsync(newUser, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Created new internal user for TarhElahi user '{ExternalUserId}'.", externalUserId);
        return newUser.Id.Value;
    }

    private async Task<Guid> UpdateUserIfNeededAsync(User user, string externalUserId, CancellationToken ct)
    {
        if (!user.NeedsProfileSync())
        {
            return user.Id.Value;
        }

        try
        {
            var strapiUser = await _tarhElahiClient.GetUserAsync(externalUserId, ct);

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

                _userRepository.Update(user);
                await _unitOfWork.SaveChangesAsync(ct);

                _logger.LogInformation("Successfully refreshed profile for user '{ExternalUserId}'.", externalUserId);
            }
            else
            {
                _logger.LogWarning("TarhElahi user '{ExternalUserId}' not found during refresh; proceeding with stale profile data.", externalUserId);
            }
        }
        catch (TarhElahiUnavailableException ex)
        {
            _logger.LogWarning(ex, "TarhElahi unavailable while refreshing profile for user '{ExternalUserId}'; proceeding with stale profile data.", externalUserId);
        }

        return user.Id.Value;
    }
}
