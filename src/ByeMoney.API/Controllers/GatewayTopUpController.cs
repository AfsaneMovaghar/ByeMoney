using ByeMoney.API.Authentication;
using ByeMoney.API.Contracts.TopUp;
using ByeMoney.API.Resources;
using ByeMoney.Application.Modules.Wallet.Commands.CancelGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmGatewayTopUp;
using ByeMoney.Application.Modules.Wallet.Commands.RecordGatewayResult;
using ByeMoney.Application.Modules.Wallet.Commands.OpenGatewayReview;
using ByeMoney.Application.Modules.Wallet.Commands.ResolveGatewayReview;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Modules.Wallet.Queries.GetGatewayTopUpDetails;
using ByeMoney.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/integrations/topups")]
public sealed class GatewayTopUpController(ISender sender, ILogger<GatewayTopUpController> logger) : ControllerBase
{
    private const string SupportedGateway = "SEP";

    [HttpPost("v1/gateway-reviews/open")]
    [ServiceFilter(typeof(GatewayResultKeyFilter))]
    [ProducesResponseType(typeof(GatewayReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GatewayErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> OpenReview(OpenGatewayReviewRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new OpenGatewayReviewCommand(
            request.ClientReferenceCode, request.CaseId, request.ReasonCode), ct);
        return result.IsSuccess ? Ok(ToReviewResponse(result.Value)) : ToErrorResult(result);
    }

    [HttpPost("v1/gateway-reviews/resolve")]
    [ServiceFilter(typeof(GatewayResultKeyFilter))]
    [ProducesResponseType(typeof(GatewayReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GatewayErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(GatewayErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ResolveReview(ResolveGatewayReviewRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new ResolveGatewayReviewCommand(
            request.ClientReferenceCode, request.CaseId, request.OutcomeCode,
            request.ResolutionFinancialReferenceId), ct);
        return result.IsSuccess ? Ok(ToReviewResponse(result.Value)) : ToErrorResult(result);
    }

    private static GatewayReviewResponse ToReviewResponse(GatewayReviewState state) => new(
        state.ClientReferenceCode, state.CaseId, state.TopUpStatus, state.ReasonCode,
        state.OpenedAtUtc, state.ResolvedAtUtc, state.OutcomeCode,
        state.ResolutionFinancialReferenceId);

    [HttpPost("gateway-results")]
    [ServiceFilter(typeof(GatewayResultKeyFilter))]
    [ProducesResponseType(typeof(GatewayResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GatewayErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(GatewayErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(GatewayErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordResult(GatewayResultRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new RecordGatewayResultCommand(
            request.ClientReferenceCode, request.Gateway, request.EventId, request.Kind,
            request.BankTransactionId, request.BankReferenceNumber, request.BankResultCode,
            request.OriginalAmountRial, request.AffectiveAmountRial, request.OccurredAtUtc), ct);
        return result.IsSuccess
            ? Ok(new GatewayResultResponse(request.ClientReferenceCode, request.Kind))
            : ToErrorResult(result);
    }

    [HttpGet("by-reference/{clientReferenceCode}/confirmation")]
    [ServiceFilter(typeof(GatewayResultKeyFilter))]
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
    [ServiceFilter(typeof(ServiceKeyFilter))]
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
    [ServiceFilter(typeof(ServiceKeyFilter))]
    [ProducesResponseType(typeof(GatewayCancellationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GatewayErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(GatewayCancellationRequest request, CancellationToken ct)
    {
        if (request.Gateway != SupportedGateway)
            return BadRequest(new GatewayErrorResponse(GatewayErrorCodes.InvalidVerificationData,
                ApiErrors.Middleware_ValidationErrorTitle));

        var result = await sender.Send(new CancelGatewayTopUpCommand(request.ClientReferenceCode, request.Gateway), ct);
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
