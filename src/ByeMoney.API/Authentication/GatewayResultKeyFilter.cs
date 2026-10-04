using System.Security.Cryptography;
using System.Text;
using ByeMoney.API.Contracts.TopUp;
using ByeMoney.API.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ByeMoney.API.Authentication;

public sealed class GatewayResultKeyFilter(IConfiguration configuration) : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var provided = context.HttpContext.Request.Headers["X-Service-Key"].ToString();
        var strapiKey = configuration["STRAPI_TO_BYEMONEY_SERVICE_KEY"];
        var byeMoneyKey = configuration["BYEMONEY_SERVICE_KEY"];

        var authorized = Matches(provided, strapiKey) ||
                         (string.IsNullOrEmpty(strapiKey) && Matches(provided, byeMoneyKey));

        if (!authorized)
            context.Result = new UnauthorizedObjectResult(new GatewayErrorResponse(
                GatewayErrorCodes.ServiceUnauthorized, ApiErrors.Middleware_UnauthorizedTitle));
    }

    private static bool Matches(string provided, string? expected)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected) ||
            Encoding.UTF8.GetByteCount(expected) < 32)
            return false;
        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
