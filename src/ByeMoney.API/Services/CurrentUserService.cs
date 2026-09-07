using System.Security.Claims;
using ByeMoney.Application.Common.Interfaces;

namespace ByeMoney.API.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity is null || !user.Identity.IsAuthenticated)
            {
                return null;
            }

            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("internal_user_id")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}

