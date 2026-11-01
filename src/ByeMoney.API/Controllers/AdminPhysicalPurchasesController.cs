using ByeMoney.API.Contracts.Purchases;
using ByeMoney.Application.Modules.Purchases.Commands.UpdatePhysicalDeliveryStatus;
using ByeMoney.Domain.Modules.Identity.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/admin/physical-purchases")]
[Authorize(Policy = PolicyNames.RequireCoursesManage)]
public sealed class AdminPhysicalPurchasesController(ISender sender) : ControllerBase
{
    /// <summary>
    /// تغییر وضعیت تحویل کالای فیزیکی؛ فقط رو به جلو: آماده‌سازی، ارسال‌شده، تحویل‌شده.
    /// </summary>
    [HttpPut("{purchaseId:guid}/delivery-status")]
    [ProducesResponseType(typeof(PhysicalDeliveryStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PhysicalDeliveryStatusResponse>> UpdateDeliveryStatus(
        Guid purchaseId,
        [FromBody] UpdatePhysicalDeliveryStatusApiRequest request,
        CancellationToken ct)
    {
        return Ok(await sender.Send(new UpdatePhysicalDeliveryStatusCommand(purchaseId, request.Status), ct));
    }
}
