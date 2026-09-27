using ByeMoney.API.Contracts.TopUp;
using ByeMoney.API.Resources;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.CreateAdminCardToCardTopUp;
using ByeMoney.Application.Modules.TarhElahiIntegration.Exceptions;
using ByeMoney.Application.Modules.Wallet.Services;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Identity.Constants;
using ByeMoney.Domain.Modules.Identity.Users;
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
        if (currentUser.UserId is not Guid actorId) return Unauthorized();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 100)
            return BadRequest(new { error = ApiErrors.TopUpRequest_InvalidIdempotencyKey });
        idempotencyKey = idempotencyKey.Trim();
        if (request.Receipt is null || request.Receipt.Length is <= 0 or > 10_000_000 || !request.Receipt.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = ApiErrors.TopUpReceipt_InvalidFile });

        string receiptId;
        try
        {
            await using var stream = request.Receipt.OpenReadStream();
            receiptId = await receipts.SaveAsync(stream, request.Receipt.FileName, idempotencyKey, ct);
        }
        catch (InvalidDataException)
        {
            return BadRequest(new { error = ApiErrors.TopUpReceipt_InvalidFile });
        }

        Result<CreateTopUpRequestResponse> result;
        try
        {
            result = await sender.Send(new CreateAdminCardToCardTopUpCommand(
                request.BeneficiaryExternalUserId, actorId, request.AmountRial, receiptId, idempotencyKey,
                request.ExternalTransactionId, ChargeType.AdminAssistedCardToCard), ct);
        }
        catch (Exception ex) when (ex is TarhElahiUnavailableException or ValidationException or DomainException)
        {
            await topUpService.DeleteUnreferencedReceiptAsync(receiptId, idempotencyKey, ct);
            throw;
        }

        if (result.IsSuccess)
            return Ok(result.Value);
        await topUpService.DeleteUnreferencedReceiptAsync(receiptId, idempotencyKey, ct);
        return result.Status switch
        {
            ResultStatus.NotFound => NotFound(new { error = result.ErrorMessage }),
            ResultStatus.Conflict => Conflict(new { error = result.ErrorMessage }),
            _ => BadRequest(new { error = result.ErrorMessage })
        };
    }
}

