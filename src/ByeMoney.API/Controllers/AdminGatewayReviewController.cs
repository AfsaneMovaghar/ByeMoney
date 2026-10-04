using ByeMoney.API.Contracts.TopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ResolveGatewayReview;
using ByeMoney.Application.Modules.Wallet.Queries.GetGatewayReview;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Authorize(Policy = PolicyNames.RequireTopUpReview)]
[Route("api/admin/topups/gateway-reviews")]
public sealed class AdminGatewayReviewController(ISender sender) : ControllerBase
{
    [HttpGet("{clientReferenceCode}")]
    public async Task<IActionResult> Get(string clientReferenceCode, CancellationToken ct)
    {
        var result = await sender.Send(new GetGatewayReviewQuery(clientReferenceCode), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("resolve")]
    [HttpPost("/api/integrations/topups/v1/gateway-reviews/resolve")]
    public async Task<IActionResult> Resolve(ResolveGatewayReviewRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new ResolveGatewayReviewCommand(request.ClientReferenceCode,
            request.CaseId, request.OutcomeCode, request.ResolutionFinancialReferenceId, request.Evidence), ct);
        if (result.IsSuccess) return Ok(result.Value);
        var response = new GatewayErrorResponse(result.ErrorCode!, result.ErrorMessage!);
        return result.Status switch
        {
            ResultStatus.NotFound => NotFound(response),
            ResultStatus.Conflict => Conflict(response),
            _ => UnprocessableEntity(response)
        };
    }
}
