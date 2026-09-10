using ByeMoney.API.Contracts.Auth;
using ByeMoney.API.Resources;
using ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;
using ByeMoney.Domain.Modules.Identity.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [Authorize]
    [HttpPost("sync")]
    public async Task<ActionResult<SyncUserResponse>> Sync(CancellationToken ct)
    {
        var externalUserId = User.FindFirst(AppClaimTypes.DocumentId)?.Value;

        if (string.IsNullOrWhiteSpace(externalUserId))
            throw new UnauthorizedAccessException(ApiErrors.Auth_DocumentIdClaimMissing);

        var cmd = new SyncUserFromStrapiCommand(externalUserId);
        var userId = await _sender.Send(cmd, ct);
        return Ok(new SyncUserResponse(userId));
    }
}
