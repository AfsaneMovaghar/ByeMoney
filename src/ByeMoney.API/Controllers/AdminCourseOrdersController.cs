using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Queries.GetAdminCourseOrders;
using ByeMoney.Domain.Modules.Identity.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/admin/course-orders")]
[Authorize(Policy = PolicyNames.RequirePurchasesView)]
public sealed class AdminCourseOrdersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PurchasePage<AdminCourseOrderListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurchasePage<AdminCourseOrderListItem>>> List(
        [FromQuery] AdminCourseOrderFilter filter, CancellationToken ct)
    {
        return Ok(await sender.Send(new GetAdminCourseOrdersQuery(filter), ct));
    }
}
