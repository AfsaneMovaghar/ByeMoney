using ByeMoney.API.Contracts.Wallet;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.CreateNoorWithdrawal;
using ByeMoney.Application.Modules.Wallet.Commands.MarkNoorWithdrawalPaid;
using ByeMoney.Application.Modules.Wallet.Commands.ReverseNoorWithdrawal;
using ByeMoney.Application.Modules.Wallet.Queries.GetNoorWithdrawal;
using ByeMoney.Application.Modules.Wallet.Queries.GetNoorWithdrawalReceipt;
using ByeMoney.Application.Modules.Wallet.Queries.GetNoorWithdrawals;
using ByeMoney.Application.Modules.Wallet.Queries.GetRefundableTopUps;
using ByeMoney.Application.Modules.Wallet.Withdrawals.Constants;
using ByeMoney.Application.Modules.Wallet.Withdrawals.DTOs;
using ByeMoney.Application.Modules.Wallet.Withdrawals.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

/// <summary>
/// خروج نور عادی کاربر توسط ادمین و ثبت واریز ریالی دستی آن (D4). مسیر کاربری برای خروج وجود ندارد (D09).
/// </summary>
[ApiController]
[Route("api/admin/noor-withdrawals")]
[Authorize(Policy = PolicyNames.RequireNoorWithdraw)]
public sealed class AdminNoorWithdrawalsController(
    ISender sender,
    IReceiptStorage receipts,
    NoorWithdrawalService withdrawals) : ControllerBase
{
    private const long MaxReceiptBytes = 10_000_000;
    private const long MaxRequestBytes = 10_500_000;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NoorWithdrawalResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NoorWithdrawalResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] string? userExternalId,
        CancellationToken ct,
        [FromQuery] int limit = GetNoorWithdrawalsQuery.DefaultLimit)
    {
        return Ok(await sender.Send(new GetNoorWithdrawalsQuery(status, userExternalId, limit), ct));
    }

    /// <summary>شارژهای تأییدشدهٔ کاربر برای انتخاب در فرم استرداد پرداخت تکراری.</summary>
    [HttpGet("refundable-topups")]
    [ProducesResponseType(typeof(IReadOnlyList<RefundableTopUpResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RefundableTopUpResponse>>> RefundableTopUps(
        [FromQuery] string userExternalId,
        CancellationToken ct)
    {
        return Ok(await sender.Send(new GetRefundableTopUpsQuery(userExternalId), ct));
    }

    [HttpGet("{withdrawalId:guid}")]
    [ProducesResponseType(typeof(NoorWithdrawalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(NoorWithdrawalErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid withdrawalId, CancellationToken ct)
    {
        var result = await sender.Send(new GetNoorWithdrawalQuery(withdrawalId), ct);
        return Respond(result);
    }

    [HttpGet("{withdrawalId:guid}/receipt")]
    public async Task<IActionResult> Receipt(Guid withdrawalId, CancellationToken ct)
    {
        var receipt = await sender.Send(new GetNoorWithdrawalReceiptQuery(withdrawalId), ct);
        if (receipt is null)
        {
            return NotFound();
        }

        var contentType = Path.GetExtension(receipt.ReceiptId).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
        return File(receipt.Content, contentType);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(NoorWithdrawalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(NoorWithdrawalErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(NoorWithdrawalErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(NoorWithdrawalErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromForm] CreateNoorWithdrawalApiRequest request, CancellationToken ct)
    {
        var saved = await SaveReceiptAsync(request.Receipt, $"noor-withdrawal:{request.OperationId:N}", ct);
        if (saved.Invalid)
        {
            return InvalidReceipt();
        }

        var command = new CreateNoorWithdrawalCommand(
            request.OperationId,
            request.UserExternalId,
            request.Kind,
            request.RelatedTopUpId,
            request.AmountRial,
            request.BeneficiaryName,
            request.BeneficiaryCardNumber,
            request.BeneficiaryIban,
            request.Description,
            request.PaymentReference,
            saved.ReceiptId);

        return await SendWithReceiptAsync(command, saved.ReceiptId, ct);
    }

    [HttpPost("{withdrawalId:guid}/payment")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(NoorWithdrawalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(NoorWithdrawalErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(NoorWithdrawalErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(NoorWithdrawalErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkPaid(
        Guid withdrawalId,
        [FromForm] MarkNoorWithdrawalPaidApiRequest request,
        CancellationToken ct)
    {
        var saved = await SaveReceiptAsync(request.Receipt, $"noor-withdrawal-payment:{withdrawalId:N}", ct);
        if (saved.Invalid)
        {
            return InvalidReceipt();
        }

        var command = new MarkNoorWithdrawalPaidCommand(
            withdrawalId, request.PaymentReference, saved.ReceiptId ?? string.Empty);

        return await SendWithReceiptAsync(command, saved.ReceiptId, ct);
    }

    [HttpPost("{withdrawalId:guid}/reverse")]
    [ProducesResponseType(typeof(NoorWithdrawalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(NoorWithdrawalErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(NoorWithdrawalErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reverse(
        Guid withdrawalId,
        [FromBody] ReverseNoorWithdrawalApiRequest request,
        CancellationToken ct)
    {
        var result = await sender.Send(new ReverseNoorWithdrawalCommand(withdrawalId, request.Reason), ct);
        return Respond(result);
    }

    /// <summary>
    /// فیش پیش از فرمان ذخیره می‌شود؛ اگر فرمان ناموفق بود یا خطا داد، فیشی که به هیچ خروجی وصل نشده پاک می‌شود.
    /// </summary>
    private async Task<IActionResult> SendWithReceiptAsync(
        IRequest<Result<NoorWithdrawalResponse>> command, string? receiptId, CancellationToken ct)
    {
        Result<NoorWithdrawalResponse> result;
        try
        {
            result = await sender.Send(command, ct);
        }
        catch
        {
            await DeleteUnreferencedReceiptAsync(receiptId);
            throw;
        }

        if (result.IsFailure)
        {
            await DeleteUnreferencedReceiptAsync(receiptId);
        }

        return Respond(result);
    }

    private async Task<SavedReceipt> SaveReceiptAsync(IFormFile? receipt, string storageKey, CancellationToken ct)
    {
        if (receipt is null)
        {
            return new SavedReceipt(null, Invalid: false);
        }

        if (receipt.Length is <= 0 or > MaxReceiptBytes ||
            !receipt.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return new SavedReceipt(null, Invalid: true);
        }

        try
        {
            await using var stream = receipt.OpenReadStream();
            var receiptId = await receipts.SaveAsync(stream, receipt.FileName, storageKey, ct);
            return new SavedReceipt(receiptId, Invalid: false);
        }
        catch (InvalidDataException)
        {
            return new SavedReceipt(null, Invalid: true);
        }
    }

    private async Task DeleteUnreferencedReceiptAsync(string? receiptId)
    {
        if (receiptId is not null)
        {
            await withdrawals.DeleteUnreferencedReceiptAsync(receiptId, CancellationToken.None);
        }
    }

    private BadRequestObjectResult InvalidReceipt() =>
        BadRequest(new NoorWithdrawalErrorResponse(NoorWithdrawalErrorCodes.InvalidReceipt, NoorWithdrawalErrors.InvalidReceipt));

    private IActionResult Respond(Result<NoorWithdrawalResponse> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        var statusCode = result.Status switch
        {
            ResultStatus.NotFound => StatusCodes.Status404NotFound,
            ResultStatus.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return StatusCode(statusCode, new NoorWithdrawalErrorResponse(
            result.ErrorCode ?? NoorWithdrawalErrorCodes.Conflict,
            result.ErrorMessage!));
    }

    private sealed record SavedReceipt(string? ReceiptId, bool Invalid);
}
