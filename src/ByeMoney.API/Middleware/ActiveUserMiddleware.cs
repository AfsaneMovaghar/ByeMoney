using System.Text.Json;
using ByeMoney.API.Contracts.Errors;
using ByeMoney.API.Resources;
using ByeMoney.Domain.Modules.Identity.Constants;
using Microsoft.AspNetCore.Authorization;

namespace ByeMoney.API.Middleware;

/// <summary>
/// مسدودسازی متمرکز درخواست‌های احراز هویت‌شده با توکن JWT برای کاربران غیرفعال یا مسدودشده
/// با کد ۴۰۳، بدون تأثیر بر مسیرهای AllowAnonymous یا احراز هویت با X-Service-Key.
/// </summary>
public sealed class ActiveUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is null
            || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || endpoint.Metadata.GetMetadata<IAuthorizeData>() is null)
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true
            && context.User.HasClaim(c => c.Type == AppClaimTypes.IsActive && c.Value == "false"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var response = new MiddlewareErrorResponse(
                ApiErrors.Middleware_ForbiddenTitle,
                StatusCodes.Status403Forbidden,
                [ApiErrors.Auth_UserInactiveMessage]);

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            return;
        }

        await next(context);
    }
}
