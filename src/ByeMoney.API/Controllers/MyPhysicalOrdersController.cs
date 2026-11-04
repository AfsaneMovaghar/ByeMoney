using ByeMoney.API.Contracts.Purchases;
using ByeMoney.Application.Modules.Purchases.Commands.CancelMyPhysicalOrder;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.Queries.GetMyPhysicalCancellationPreview;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/me/physical-orders")]
[Authorize]
public sealed class MyPhysicalOrdersController(ISender sender) : ControllerBase
{
    /// <summary>پیش‌نمایش بدون اثر لغو کل بخش فیزیکی سبد، همراه هشدار حذف کوپن و ارسال رایگان.</summary>
    [HttpGet("{cartId:guid}/cancellation-preview")]
    [ProducesResponseType(typeof(PhysicalCancellationPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PhysicalCancellationErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(PhysicalCancellationErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Preview(Guid cartId, CancellationToken ct)
    {
        var result = await sender.Send(new GetMyPhysicalCancellationPreviewQuery(cartId), ct);
        return PhysicalCancellationHttp.Respond(this, result);
    }

    /// <summary>لغو کل بخش فیزیکی سبد خود کاربر پیش از ارسال؛ تکرار با همان شناسهٔ عملیات همان نتیجه را می‌دهد.</summary>
    [HttpPost("{cartId:guid}/cancel")]
    [ProducesResponseType(typeof(PhysicalCancellationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PhysicalCancellationErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(PhysicalCancellationErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(PhysicalCancellationErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Cancel(
        Guid cartId,
        [FromBody] CancelMyPhysicalOrderApiRequest request,
        CancellationToken ct)
    {
        var command = new CancelMyPhysicalOrderCommand(cartId, request.OperationId, request.Reason);
        var result = await sender.Send(command, ct);
        return PhysicalCancellationHttp.Respond(this, result);
    }
}
