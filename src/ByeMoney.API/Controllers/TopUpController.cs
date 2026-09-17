using ByeMoney.API.Contracts.TopUp;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.CreateTopUpRequest;
using ByeMoney.Application.Resources;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/topup")]
[Authorize]
public class TopUpController(ISender sender, ICurrentUserService currentUserService) : ControllerBase
{
    private readonly ISender _sender = sender;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    /// <summary>
    /// Creates a new top-up request for the authenticated user.
    /// </summary>
    [HttpPost("requests")]
    [ProducesResponseType(typeof(CreateTopUpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateRequest(
        [FromBody] CreateTopUpApiRequest request,
        CancellationToken ct)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        var command = new CreateTopUpRequestCommand(
            userId,
            request.Amount,
            request.PaymentMethod,
            request.ExternalTransactionId,
            request.PendingItemType,
            request.PendingItemExternalId,
            request.PendingPriceSnapshot,
            request.PendingRateSnapshot);

        var result = await _sender.Send(command, ct);

        return Ok(new CreateTopUpResponse(result.TopUpRequestId, result.ClientReferenceId));
    }
}

