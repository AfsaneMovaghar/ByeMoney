using ByeMoney.API.Contracts.Errors;
using ByeMoney.API.Contracts.Gift;
using ByeMoney.API.Resources;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.AdminGrantGiftNoor;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Identity.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = PolicyNames.RequireNoorGift)]
public sealed class AdminGiftController(
    ISender sender,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpPost("noor-gifts")]
    [ProducesResponseType(typeof(AdminGrantGiftNoorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AdminGiftErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MiddlewareErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GrantGiftNoor(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] AdminGrantGiftNoorApiRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new AdminGiftErrorResponse(
                ApplicationErrors.Gift_IdempotencyKeyRequired,
                StatusCodes.Status400BadRequest,
                [ApplicationErrors.Gift_IdempotencyKeyRequired]));
        }

        var command = new AdminGrantGiftNoorCommand(
            request.ExternalUserIds,
            request.AmountNoor,
            request.Reason,
            idempotencyKey,
            currentUserService.UserId);

        var result = await sender.Send(command, ct);

        if (result.IsSuccess)
        {
            return Ok(result.Response);
        }

        if (result.HasInvalidUsers)
        {
            return BadRequest(new AdminGiftErrorResponse(
                result.ErrorMessage ?? ApplicationErrors.Gift_HasInvalidUsers,
                StatusCodes.Status400BadRequest,
                [result.ErrorMessage ?? ApplicationErrors.Gift_HasInvalidUsers],
                result.InvalidUsers));
        }

        if (result.IsConflict)
        {
            return Conflict(new MiddlewareErrorResponse(
                result.ErrorMessage ?? ApplicationErrors.TopUpRequest_IdempotencyConflict,
                StatusCodes.Status409Conflict,
                [result.ErrorMessage ?? ApplicationErrors.TopUpRequest_IdempotencyConflict]));
        }

        return BadRequest(new AdminGiftErrorResponse(
            result.ErrorMessage ?? ApiErrors.Middleware_UnexpectedErrorMessage,
            StatusCodes.Status400BadRequest,
            [result.ErrorMessage ?? ApiErrors.Middleware_UnexpectedErrorMessage]));
    }
}

