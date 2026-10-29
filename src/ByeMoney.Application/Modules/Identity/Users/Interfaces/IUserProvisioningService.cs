using ByeMoney.Domain.Modules.Identity.Users;

namespace ByeMoney.Application.Modules.Identity.Users.Interfaces;

/// <summary>
/// نتیجهٔ پیدا کردن یا ساختن کاربر داخلی بر اساس شناسهٔ خارجی (documentId استرپی).
/// Created فقط زمانی true است که کاربر در همین فراخوانی واقعاً ساخته شده باشد.
/// </summary>
public sealed record UserProvisioningResult(User User, bool Created);

public interface IUserProvisioningService
{
    /// <summary>
    /// کاربر داخلی را با شناسهٔ خارجی پیدا می‌کند؛ اگر نبود، از استرپی پروفایل را می‌گیرد،
    /// کاربر را می‌سازد و هدیهٔ خوش‌آمد را اعطا می‌کند.
    /// اگر کاربر در استرپی یافت نشود NotFoundException و اگر سرویس استرپی در دسترس نباشد
    /// TarhElahiUnavailableException پرتاب می‌شود.
    /// </summary>
    Task<UserProvisioningResult> EnsureUserFromExternalAsync(string externalUserId, CancellationToken ct = default);
}
