using ByeMoney.API.Contracts.Purchases;
using ByeMoney.Application.Modules.Purchases.Commands.AdminCancelPhysicalPurchase;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminPhysicalCancellationPreview;
using ByeMoney.Domain.Modules.Identity.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

/// <summary>
/// لغو قلم یا تعداد کالای فیزیکی با برگشت نور. مجوز تزریق نور لازم است، چون برگشت زیرمجموعهٔ همان اختیار افزایش نور است.
/// </summary>
[ApiController]
[Route("api/admin/physical-purchases")]
[Authorize(Policy = PolicyNames.RequireNoorInject)]
public sealed class AdminPhysicalCancellationsController(ISender sender) : ControllerBase
{
    [HttpGet("{purchaseId:guid}/cancellation-preview")]
    [ProducesResponseType(typeof(PhysicalCancellationPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PhysicalCancellationErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(PhysicalCancellationErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Preview(Guid purchaseId, [FromQuery] int quantity, CancellationToken ct)
    {
        var result = await sender.Send(new GetAdminPhysicalCancellationPreviewQuery(purchaseId, quantity), ct);
        return PhysicalCancellationHttp.Respond(this, result);
    }

    [HttpPost("{purchaseId:guid}/cancel")]
    [ProducesResponseType(typeof(PhysicalCancellationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PhysicalCancellationErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(PhysicalCancellationErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid purchaseId,
        [FromBody] AdminCancelPhysicalPurchaseApiRequest request,
        CancellationToken ct)
    {
        var command = new AdminCancelPhysicalPurchaseCommand(
            purchaseId, request.Quantity, request.OperationId, request.Reason);
        var result = await sender.Send(command, ct);
        return PhysicalCancellationHttp.Respond(this, result);
    }
}
