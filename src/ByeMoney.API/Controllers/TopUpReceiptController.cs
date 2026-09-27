using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Modules.Identity.Constants;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/topup/requests/{topUpId:guid}/receipt")]
[Authorize]
public class TopUpReceiptController(
    ITopUpRequestRepository topUps,
    IReceiptStorage receipts,
    ICurrentUserService currentUser,
    IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid topUpId, CancellationToken ct)
    {
        var topUp = await topUps.GetByIdAsync(new TopUpRequestId(topUpId), ct);
        if (topUp?.ReceiptId is null) return NotFound();
        var isBeneficiary = currentUser.UserId == topUp.UserId.Value;
        if (!isBeneficiary)
        {
            var isAuthorizedAdmin = User is not null && (await authorization.AuthorizeAsync(User, PolicyNames.RequireTopUpReview)).Succeeded;
            if (!isAuthorizedAdmin) return NotFound();
        }
        var stream = await receipts.OpenReadAsync(topUp.ReceiptId, ct);
        if (stream is null) return NotFound();
        var contentType = Path.GetExtension(topUp.ReceiptId).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
        return File(stream, contentType);
    }
}
