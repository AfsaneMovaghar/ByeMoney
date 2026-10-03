using System.Security.Cryptography;
using System.Text;
using ByeMoney.API.Resources;
using ByeMoney.API.Contracts.TopUp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ByeMoney.API.Authentication;

public sealed class ServiceKeyFilter(IConfiguration configuration) : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var expected = configuration["BYEMONEY_SERVICE_KEY"];
        var provided = context.HttpContext.Request.Headers["X-Service-Key"].ToString();
        if (string.IsNullOrEmpty(expected) || Encoding.UTF8.GetByteCount(expected) < 32 ||
            string.IsNullOrEmpty(provided) || !Matches(provided, expected))
            context.Result = new UnauthorizedObjectResult(new GatewayErrorResponse(
                GatewayErrorCodes.ServiceUnauthorized, ApiErrors.Middleware_UnauthorizedTitle));
    }

    private static bool Matches(string provided, string expected)
    {
        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
