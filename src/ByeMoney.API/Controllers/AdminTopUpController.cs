using ByeMoney.API.Contracts.TopUp;
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
[Authorize(Policy = PolicyNames.RequireTopUpReview)]
public class AdminTopUpController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    /// <summary>
    /// Lookup a TopUpRequest by its ClientReferenceCode.
    /// </summary>
    [HttpGet("by-reference/{clientReferenceCode}")]
    public async Task<IActionResult> GetByClientReferenceCode(
        string clientReferenceCode, CancellationToken ct)
    {
        var result = await _sender.Send(
            new GetTopUpByClientReferenceCodeQuery(clientReferenceCode), ct);

        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Confirm a pending TopUpRequest (admin review).
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
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
    /// Reject a pending TopUpRequest (admin review).
    /// </summary>
    [HttpPost("{id:guid}/reject")]
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
                _ => BadRequest(new { error = result.ErrorMessage })
            };
    }
}
