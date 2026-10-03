using ByeMoney.API.Authentication;
using ByeMoney.API.Contracts.TopUp;
using ByeMoney.API.Resources;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ReportGatewayCancellation;
using ByeMoney.Application.Modules.Wallet.Queries.GetGatewayTopUpDetails;
using ByeMoney.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[AllowAnonymous]
[ServiceFilter(typeof(ServiceKeyFilter))]
[Route("api/integrations/topups")]
public sealed class GatewayTopUpController(ISender sender, ILogger<GatewayTopUpController> logger) : ControllerBase
{
    private const string SupportedGateway = "SEP";

    [HttpGet("by-reference/{clientReferenceCode}/confirmation")]
    [ProducesResponseType(typeof(GatewayTopUpDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GatewayErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConfirmation(string clientReferenceCode, CancellationToken ct)
    {
        var topUp = await sender.Send(new GetGatewayTopUpDetailsQuery(clientReferenceCode), ct);
        if (topUp is null)
            return NotFound(new GatewayErrorResponse(GatewayErrorCodes.TopUpNotFound, ApiErrors.Middleware_NotFoundTitle));

        return Ok(new GatewayTopUpDetailsResponse(
            topUp.TopUpRequestId, topUp.ClientReferenceCode, topUp.AmountRial,
            topUp.Status, topUp.PaymentMethod, topUp.ExternalTransactionId,
            topUp.BankReferenceNumber, topUp.GatewayName));
    }

    [HttpPost("gateway-confirmations")]
    [ProducesResponseType(typeof(GatewayConfirmationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GatewayErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(GatewayErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Confirm(GatewayConfirmationRequest request, CancellationToken ct)
    {
        if (request.Gateway != SupportedGateway)
            return BadRequest(new GatewayErrorResponse(GatewayErrorCodes.InvalidVerificationData,
                ApiErrors.Middleware_ValidationErrorTitle));

        var outcome = await sender.Send(new ConfirmGatewayTopUpCommand(
            request.ClientReferenceCode, request.Gateway, request.ExternalTransactionId,
            request.BankReferenceNumber, request.OriginalAmountRial,
            request.AffectiveAmountRial), ct);

        logger.LogInformation(
            "Gateway TopUp confirmation: {ClientReferenceCode} {Gateway} {ExternalTransactionId} {BankReferenceNumber} {Status}",
            request.ClientReferenceCode, request.Gateway, request.ExternalTransactionId,
            request.BankReferenceNumber, outcome.Result.Status);

        return outcome.Result.IsSuccess
            ? Ok(new GatewayConfirmationResponse("confirmed", request.ClientReferenceCode,
                outcome.Idempotent ? true : null))
            : ToErrorResult(outcome.Result);
    }

    /// <summary>
    /// ثبت انصراف قطعی از پرداخت پس از اطمینان سرویس درگاه از انجام‌نشدن پرداخت.
    /// </summary>
    [HttpPost("gateway-cancellations")]
    [ProducesResponseType(typeof(GatewayCancellationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GatewayErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(GatewayCancellationRequest request, CancellationToken ct)
    {
        if (request.Gateway != SupportedGateway)
            return BadRequest(new GatewayErrorResponse(GatewayErrorCodes.InvalidVerificationData,
                ApiErrors.Middleware_ValidationErrorTitle));

        var result = await sender.Send(new ReportGatewayCancellationCommand(request.ClientReferenceCode), ct);
        return result.IsSuccess
            ? Ok(new GatewayCancellationResponse("rejected", request.ClientReferenceCode))
            : ToErrorResult(result);
    }

    private IActionResult ToErrorResult(Result result)
    {
        var response = new GatewayErrorResponse(
            result.ErrorCode ?? GatewayErrorCodes.InvalidVerificationData,
            result.ErrorMessage ?? ApiErrors.Middleware_ValidationErrorTitle);
        return result.Status switch
        {
            ResultStatus.NotFound => NotFound(response),
            ResultStatus.Conflict => Conflict(response),
            _ => UnprocessableEntity(response)
        };
    }
}
