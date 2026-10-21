using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.API.Contracts.TopUp;
using ByeMoney.API.Resources;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.CreateAdminCardToCardTopUp;
using ByeMoney.Application.Modules.TarhElahiIntegration.Exceptions;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Constants;
using ByeMoney.Domain.Modules.Wallet.TopUps;
using MediatR;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/admin/topups")]
[Authorize(Policy = PolicyNames.RequireTopUpReview)]
public class AdminAssistedTopUpController(
    ISender sender,
    ICurrentUserService currentUser,
    IReceiptStorage receipts,
    IAdminAssistedTopUpService topUpService) : ControllerBase
{
    [HttpPost("assisted")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_500_000)]
    public async Task<IActionResult> Create(
        [FromForm] AdminAssistedTopUpApiRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actorId)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 100)
        {
            return BadRequest(new AdminAssistedTopUpErrorResponse(ApiErrors.TopUpRequest_InvalidIdempotencyKey));
        }

        idempotencyKey = idempotencyKey.Trim();
        string? receiptId = null;
        if (request.Receipt is { } receipt)
        {
            if (receipt.Length is <= 0 or > 10_000_000 ||
                !receipt.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new AdminAssistedTopUpErrorResponse(ApiErrors.TopUpReceipt_InvalidFile));
            }

            try
            {
                await using var stream = receipt.OpenReadStream();
                receiptId = await receipts.SaveAsync(stream, receipt.FileName, idempotencyKey, ct);
            }
            catch (InvalidDataException)
            {
                return BadRequest(new AdminAssistedTopUpErrorResponse(ApiErrors.TopUpReceipt_InvalidFile));
            }
        }

        Result<CreateTopUpRequestResponse> result;
        try
        {
            result = await sender.Send(new CreateAdminCardToCardTopUpCommand(
                request.BeneficiaryExternalUserId, actorId, request.AmountToman, receiptId, idempotencyKey,
                request.ExternalTransactionId, ChargeType.AdminAssistedCardToCard), ct);
        }
        catch (Exception ex) when (ex is TarhElahiUnavailableException or ValidationException or DomainException or InvalidOperationException)
        {
            if (receiptId is not null)
            {
                await topUpService.DeleteUnreferencedReceiptAsync(receiptId, idempotencyKey, ct);
            }
            throw;
        }

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        if (receiptId is not null)
        {
            await topUpService.DeleteUnreferencedReceiptAsync(receiptId, idempotencyKey, ct);
        }

        return result.Status switch
        {
            ResultStatus.NotFound => NotFound(new AdminAssistedTopUpErrorResponse(result.ErrorMessage)),
            ResultStatus.Conflict => Conflict(new AdminAssistedTopUpErrorResponse(result.ErrorMessage)),
            _ => BadRequest(new AdminAssistedTopUpErrorResponse(result.ErrorMessage))
        };
    }
}

