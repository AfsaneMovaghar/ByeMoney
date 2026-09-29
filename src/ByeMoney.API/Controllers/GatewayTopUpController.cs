using ByeMoney.API.Authentication;
using ByeMoney.API.Contracts.TopUp;
using ByeMoney.API.Resources;
using ByeMoney.Application.Modules.Wallet.Commands.ConfirmTopUp;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ByeMoney.API.Controllers;

[ApiController]
[AllowAnonymous]
[ServiceFilter(typeof(ServiceKeyFilter))]
[Route("api/integrations/topups")]
public sealed class GatewayTopUpController(
    ISender sender,
    ITopUpRequestRepository topUps,
    IServiceScopeFactory scopes,
    ILogger<GatewayTopUpController> logger) : ControllerBase
{
    private const string SupportedGateway = "SEP";
    private const string PostgresUniqueViolationCode = "23505";

    [HttpGet("by-reference/{clientReferenceCode}/confirmation")]
    public async Task<IActionResult> GetConfirmation(string clientReferenceCode, CancellationToken ct)
    {
        var topUp = await topUps.GetByClientReferenceCodeAsync(clientReferenceCode, ct);
        if (topUp is null)
            return NotFound(new GatewayErrorResponse(GatewayErrorCodes.TopUpNotFound, ApiErrors.Middleware_NotFoundTitle));

        return Ok(new GatewayTopUpDetailsResponse(
            topUp.Id.Value,
            topUp.ClientReferenceCode,
            topUp.AmountRial,
            topUp.Status.ToString(),
            topUp.PaymentMethod.ToString(),
            topUp.ExternalTransactionId,
            topUp.BankReferenceNumber,
            topUp.GatewayName));
    }

    [HttpPost("gateway-confirmations")]
    public async Task<IActionResult> Confirm(GatewayConfirmationRequest request, CancellationToken ct)
    {
        if (request.Gateway != SupportedGateway)
            return BadRequest(new GatewayErrorResponse(GatewayErrorCodes.InvalidVerificationData, ApiErrors.Middleware_ValidationErrorTitle));

        try
        {
            var command = new ConfirmTopUpCommand(
                new TopUpRequestId(Guid.Empty),
                request.ExternalTransactionId,
                0m,
                request.ClientReferenceCode,
                request.Gateway,
                request.BankReferenceNumber,
                request.OriginalAmountRial,
                request.AffectiveAmountRial);

            var result = await sender.Send(command, ct);

            logger.LogInformation(
                "Gateway TopUp confirmation: {ClientReferenceCode} {Gateway} {ExternalTransactionId} {BankReferenceNumber} {Status}",
                request.ClientReferenceCode, request.Gateway, request.ExternalTransactionId,
                request.BankReferenceNumber, result.Status);

            return ToActionResult(result, request.ClientReferenceCode);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await HandleConcurrencyConflictAsync(request, ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresUniqueViolationCode })
        {
            return Conflict(new GatewayErrorResponse(GatewayErrorCodes.RefNumConflict, ApiErrors.Middleware_ConflictTitle));
        }
    }

    private async Task<IActionResult> HandleConcurrencyConflictAsync(GatewayConfirmationRequest request, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var fresh = await scope.ServiceProvider.GetRequiredService<ITopUpRequestRepository>()
            .GetByClientReferenceCodeAsync(request.ClientReferenceCode, ct);

        if (fresh is { Status: TopUpStatus.Confirmed } &&
            fresh.ExternalTransactionId == request.ExternalTransactionId &&
            fresh.BankReferenceNumber == request.BankReferenceNumber &&
            fresh.AmountRial == request.OriginalAmountRial &&
            fresh.AmountRial == request.AffectiveAmountRial)
        {
            return Ok(new { status = "confirmed", clientReferenceCode = request.ClientReferenceCode, idempotent = true });
        }

        return Conflict(new GatewayErrorResponse(GatewayErrorCodes.TopUpConcurrentConfirmation, ApiErrors.Middleware_ConflictTitle));
    }

    private IActionResult ToActionResult(Result result, string clientReferenceCode)
    {
        if (result.IsSuccess)
            return Ok(new { status = "confirmed", clientReferenceCode });

        var errorCode = ResolveErrorCode(result);
        var response = new GatewayErrorResponse(errorCode, result.ErrorMessage ?? ApiErrors.Middleware_ValidationErrorTitle);

        return result.Status switch
        {
            ResultStatus.NotFound => NotFound(response),
            ResultStatus.Conflict => Conflict(response),
            _ => UnprocessableEntity(response)
        };
    }

    private static string ResolveErrorCode(Result result)
    {
        return result.Status switch
        {
            ResultStatus.NotFound => GatewayErrorCodes.TopUpNotFound,
            ResultStatus.Conflict when result.ErrorMessage == ByeMoney.Domain.Resources.DomainErrors.TopUpRequest_CannotConfirmRejected
                => GatewayErrorCodes.TopUpRequiresReview,
            ResultStatus.Conflict when result.ErrorMessage == ByeMoney.Application.Resources.ApplicationErrors.TopUpRequest_PaymentMethodInvalid
                => GatewayErrorCodes.InvalidVerificationData,
            ResultStatus.Conflict => GatewayErrorCodes.RefNumConflict,
            _ => GatewayErrorCodes.TopUpAmountMismatch
        };
    }
}

