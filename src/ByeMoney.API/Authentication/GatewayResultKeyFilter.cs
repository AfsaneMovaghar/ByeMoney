using System.Security.Cryptography;
using System.Text;
using ByeMoney.API.Contracts.TopUp;
using ByeMoney.API.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ByeMoney.API.Authentication;

/// <summary>
/// احراز هویت مسیرهای نتیجه و بازیابی درگاه با دو کلید جدا (استرپی و سرور Next).
/// فراخواننده فقط از روی کلید تطبیق‌خورده تعیین و در لاگ ثبت می‌شود.
/// </summary>
public sealed class GatewayResultKeyFilter(
    IConfiguration configuration,
    ILogger<GatewayResultKeyFilter> logger) : IAuthorizationFilter
{
    public const string StrapiKeyName = "STRAPI_TO_BYEMONEY_SERVICE_KEY";
    public const string FrontendKeyName = "FRONTEND_TO_BYEMONEY_SERVICE_KEY";
    public const string CallerItemKey = "GatewayCaller";

    private const string ServiceKeyHeader = "X-Service-Key";
    private const int MinimumKeyBytes = 32;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var provided = context.HttpContext.Request.Headers[ServiceKeyHeader].ToString();
        var caller = ResolveCaller(provided);

        if (caller is null)
        {
            logger.LogWarning(
                "Gateway route rejected: no valid service key. Path {Path}",
                context.HttpContext.Request.Path);
            context.Result = new UnauthorizedObjectResult(new GatewayErrorResponse(
                GatewayErrorCodes.ServiceUnauthorized, ApiErrors.Middleware_UnauthorizedTitle));
            return;
        }

        context.HttpContext.Items[CallerItemKey] = caller.Value;
        logger.LogInformation(
            "Gateway route called by {Caller}. Path {Path}",
            caller.Value,
            context.HttpContext.Request.Path);
    }

    private GatewayCaller? ResolveCaller(string provided)
    {
        // هر دو کلید همیشه مقایسه می‌شوند تا زمان پاسخ به تطبیق کلید اول وابسته نشود.
        var isStrapi = Matches(provided, configuration[StrapiKeyName]);
        var isFrontend = Matches(provided, configuration[FrontendKeyName]);

        if (isStrapi)
        {
            return GatewayCaller.Strapi;
        }

        return isFrontend ? GatewayCaller.Frontend : null;
    }

    private static bool Matches(string provided, string? expected)
    {
        if (string.IsNullOrEmpty(provided) ||
            string.IsNullOrEmpty(expected) ||
            Encoding.UTF8.GetByteCount(expected) < MinimumKeyBytes)
        {
            return false;
        }

        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
