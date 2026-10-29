using System.Net;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Identity.Users.Interfaces;
using ByeMoney.Application.Modules.TarhElahiIntegration.DTOs;
using ByeMoney.Application.Modules.TarhElahiIntegration.Exceptions;
using ByeMoney.Application.Modules.TarhElahiIntegration.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ByeMoney.Infrastructure.Modules.Identity.Services;

/// <summary>
/// نقطهٔ واحد ساخت کاربر داخلی از استرپی. مدیریت تداخل یکتایی و خطاهای درایور دیتابیس
/// در همین سرویس زیرساختی محصور می‌شود تا handlerها فقط هماهنگی تجاری را انجام دهند.
/// </summary>
public sealed class UserProvisioningService(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ITarhElahiIntegrationClient tarhElahiClient,
    IWelcomeGiftService welcomeGiftService,
    ApplicationDbContext context,
    ILogger<UserProvisioningService> logger) : IUserProvisioningService
{
    /// <summary>تأخیر کوتاه پیش از تلاش مجدد برای خطاهای موقت استرپی.</summary>
    private static readonly TimeSpan TransientRetryDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>نام قید یکتای شناسهٔ خارجی کاربر در مهاجرت EF Core.</summary>
    private const string ExternalUserIdUniqueIndexName = "IX_Users_ExternalUserId";

    public async Task<UserProvisioningResult> EnsureUserFromExternalAsync(string externalUserId, CancellationToken ct = default)
    {
        var existing = await userRepository.GetByExternalUserIdAsync(externalUserId, ct);
        if (existing is not null)
        {
            return new UserProvisioningResult(existing, Created: false);
        }

        var strapiUser = await GetUserFromStrapiWithTransientRetryAsync(externalUserId, ct);

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
            blocked: strapiUser.Blocked);

        await userRepository.AddAsync(newUser, ct);
        await welcomeGiftService.GrantWelcomeGiftIfEligibleAsync(newUser, ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsExternalUserIdUniqueConflict(ex))
        {
            // درخواست هم‌زمان کاربر را ساخته است؛ همهٔ تغییرات معلق همین درخواست (کاربر، کیف پول،
            // سطرهای دفتر کل و هدیه) دور ریخته می‌شود تا در SaveChanges بعدی نوشته نشوند و
            // هدیه فقط برای سازندهٔ واقعی باقی بماند.
            context.ChangeTracker.Clear();

            var winner = await userRepository.GetByExternalUserIdAsync(externalUserId, ct);
            if (winner is null)
            {
                throw;
            }

            logger.LogInformation(
                "Lost concurrent user creation race for TarhElahi user '{ExternalUserId}'; returning the existing user without a second welcome gift.",
                externalUserId);

            return new UserProvisioningResult(winner, Created: false);
        }

        logger.LogInformation("Created new internal user for TarhElahi user '{ExternalUserId}'.", externalUserId);
        return new UserProvisioningResult(newUser, Created: true);
    }

    private async Task<TarhElahiUserDto?> GetUserFromStrapiWithTransientRetryAsync(string externalUserId, CancellationToken ct)
    {
        try
        {
            return await tarhElahiClient.GetUserAsync(externalUserId, ct);
        }
        catch (TarhElahiUnavailableException ex) when (IsTransientFailure(ex))
        {
            logger.LogWarning(
                ex,
                "Transient TarhElahi failure while fetching user '{ExternalUserId}'; retrying once.",
                externalUserId);

            await Task.Delay(TransientRetryDelay, ct);
        }

        return await tarhElahiClient.GetUserAsync(externalUserId, ct);
    }

    /// <summary>
    /// فقط قطع اتصال، timeout و خطاهای سرور (۵xx) موقت هستند؛ خطاهای احراز هویت و
    /// ۴xx تلاش مجدد ندارند و نبود کاربر در استرپی (۴۰۴) اساساً استثنا ایجاد نمی‌کند.
    /// </summary>
    private static bool IsTransientFailure(TarhElahiUnavailableException ex)
        => ex.StatusCode is null
           || ex.StatusCode >= HttpStatusCode.InternalServerError;

    private static bool IsExternalUserIdUniqueConflict(DbUpdateException ex)
        => ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ExternalUserIdUniqueIndexName
        };
}
