using System.Security.Claims;
using ByeMoney.API.Resources;
using ByeMoney.Application.Modules.Identity.Authorization;
using Microsoft.AspNetCore.Authentication;

namespace ByeMoney.API.Authentication;

public class RbacClaimsTransformation(IUserAuthorizationService userAuthorizationService) : IClaimsTransformation
{
    private readonly IUserAuthorizationService _userAuthorizationService = userAuthorizationService;
    private const string TransformedMarkerClaimType = "urn:byemoney:transformed";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is null || !principal.Identity.IsAuthenticated)
        {
            return principal;
        }

        if (principal.HasClaim(c => c.Type == TransformedMarkerClaimType))
        {
            return principal;
        }

        var externalUserId = principal.FindFirst("documentId")?.Value;

        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            throw new UnauthorizedAccessException(ApiErrors.Auth_DocumentIdClaimMissing);
        }

        var userAuth = await _userAuthorizationService.GetPermissionsByExternalUserIdAsync(externalUserId);
        if (userAuth is null)
        {
            return principal;
        }

        var claimsIdentity = new ClaimsIdentity();
        claimsIdentity.AddClaim(new Claim(TransformedMarkerClaimType, "true"));
        claimsIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userAuth.UserId.ToString()));
        claimsIdentity.AddClaim(new Claim("internal_user_id", userAuth.UserId.ToString()));

        foreach (var role in userAuth.Roles)
        {
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in userAuth.Permissions)
        {
            claimsIdentity.AddClaim(new Claim("permission", permission));
        }

        principal.AddIdentity(claimsIdentity);
        return principal;
    }
}

