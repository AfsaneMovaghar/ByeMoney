using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminCoursePurchaseReceipt;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminCoursePurchases;
using ByeMoney.Domain.Modules.Identity.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/admin/course-purchases")]
[Authorize(Policy = PolicyNames.RequirePurchasesView)]
public sealed class AdminCoursePurchasesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HttpGet("/api/admin/purchases")]
    [ProducesResponseType(typeof(PurchasePage<AdminPurchaseListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchasePage<AdminPurchaseListItem>>> List(
        [FromQuery] PurchaseHistoryFilter filter, CancellationToken ct)
    {
        return Ok(await sender.Send(new GetAdminCoursePurchasesQuery(filter), ct));
    }

    [HttpGet("{purchaseId:guid}")]
    [HttpGet("/api/admin/purchases/{purchaseId:guid}")]
    [ProducesResponseType(typeof(PurchaseReceipt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseReceipt>> Receipt(Guid purchaseId, CancellationToken ct)
    {
        return Ok(await sender.Send(new GetAdminCoursePurchaseReceiptQuery(purchaseId), ct));
    }
}
