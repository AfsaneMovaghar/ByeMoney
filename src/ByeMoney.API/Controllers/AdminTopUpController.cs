using ByeMoney.API.Contracts.TopUp;
using ByeMoney.Application.Modules.Identity.Queries.GetUserPermissions;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.RejectTopUp;
using ByeMoney.Application.Modules.Wallet.Queries.GetTopUpByClientReferenceCode;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Constants;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/admin/topups")]
[Authorize]
public class AdminTopUpController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    /// <summary>
    /// استعلام نقش‌ها و مجوزهای کاربر جاری در بای‌مانی برای نمایش امکانات مجاز فرانت‌اند.
    /// </summary>
    [HttpGet("permissions")]
    [ProducesResponseType(typeof(UserPermissionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPermissions(CancellationToken ct)
    {
        var externalUserId = User.FindFirst(AppClaimTypes.DocumentId)?.Value;
        if (string.IsNullOrWhiteSpace(externalUserId))
            return Unauthorized();

        var result = await _sender.Send(new GetUserPermissionsQuery(externalUserId), ct);
        return result is null ? Unauthorized() : Ok(result);
    }

    /// <summary>
    /// جست‌وجوی درخواست شارژ با کد پیگیری.
    /// </summary>
    [HttpGet("by-reference/{clientReferenceCode}")]
    [Authorize(Policy = PolicyNames.RequireTopUpReview)]
    public async Task<IActionResult> GetByClientReferenceCode(
        string clientReferenceCode, CancellationToken ct)
    {
        var result = await _sender.Send(
            new GetTopUpByClientReferenceCodeQuery(clientReferenceCode), ct);

        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// تأیید درخواست شارژ معلق توسط ادمین.
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = PolicyNames.RequireTopUpReview)]
    public async Task<IActionResult> Confirm(
        Guid id, [FromBody] AdminConfirmRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(
            new ConfirmTopUpCommand(
                new TopUpRequestId(id),
                request.ExternalTransactionId,
                request.ConfirmedAmount), ct);

        return result.IsSuccess
            ? NoContent()
            : result.Status switch
            {
                ResultStatus.NotFound => NotFound(new { error = result.ErrorMessage }),
                ResultStatus.Conflict => Conflict(new { error = result.ErrorMessage }),
                _ => BadRequest(new { error = result.ErrorMessage })
            };
    }

    /// <summary>
    /// رد درخواست شارژ معلق توسط ادمین.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = PolicyNames.RequireTopUpReview)]
    public async Task<IActionResult> Reject(
        Guid id, [FromBody] AdminRejectRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(
            new RejectTopUpCommand(id, request.Reason), ct);

        return result.IsSuccess
            ? NoContent()
            : result.Status switch
            {
                ResultStatus.NotFound => NotFound(new { error = result.ErrorMessage }),
                ResultStatus.Conflict => Conflict(new { error = result.ErrorMessage }),
                _ => BadRequest(new { error = result.ErrorMessage })
            };
    }
}
