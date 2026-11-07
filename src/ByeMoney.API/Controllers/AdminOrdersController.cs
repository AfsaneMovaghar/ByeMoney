using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminOrder;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminOrderReceipt;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminOrders;
using ByeMoney.Domain.Modules.Identity.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Policy = PolicyNames.RequirePurchasesView)]
public sealed class AdminOrdersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PurchasePage<AdminOrderListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchasePage<AdminOrderListItem>>> List(
        [FromQuery] AdminOrderFilter filter, CancellationToken ct)
    {
        return Ok(await sender.Send(new GetAdminOrdersQuery(filter), ct));
    }

    [HttpGet("{orderId}")]
    [ProducesResponseType(typeof(AdminOrderDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminOrderDetails>> Details(string orderId, CancellationToken ct)
    {
        return Ok(await sender.Send(new GetAdminOrderQuery(orderId), ct));
    }

    [HttpGet("{orderId}/receipt")]
    [ProducesResponseType(typeof(OrderReceipt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderReceipt>> Receipt(string orderId, CancellationToken ct)
    {
        return Ok(await sender.Send(new GetAdminOrderReceiptQuery(orderId), ct));
    }
}
