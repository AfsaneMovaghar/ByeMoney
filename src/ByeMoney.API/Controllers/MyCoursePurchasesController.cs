using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Queries.GetMyCoursePurchaseReceipt;
using ByeMoney.Application.Modules.Purchases.Queries.GetMyCoursePurchases;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/me/course-purchases")]
[Authorize]
public sealed class MyCoursePurchasesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HttpGet("/api/purchases")]
    [ProducesResponseType(typeof(PurchasePage<PurchaseListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchasePage<PurchaseListItem>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await sender.Send(new GetMyCoursePurchasesQuery(page, pageSize), ct));
    }

    [HttpGet("{purchaseId:guid}")]
    [HttpGet("/api/purchases/{purchaseId:guid}")]
    [ProducesResponseType(typeof(PurchaseReceipt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseReceipt>> Receipt(Guid purchaseId, CancellationToken ct)
    {
        return Ok(await sender.Send(new GetMyCoursePurchaseReceiptQuery(purchaseId), ct));
    }
}
