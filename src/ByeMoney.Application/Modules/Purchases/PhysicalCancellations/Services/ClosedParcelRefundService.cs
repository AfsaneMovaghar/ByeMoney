using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Constants;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;
using ByeMoney.Application.Modules.Wallet.DTOs;
using ByeMoney.Application.Modules.Wallet.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Users;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet;
using ByeMoney.Domain.Modules.Wallet.Ledgers;

namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;

/// <summary>
/// برگشت دستی و تکرارپذیر نور برای قلم بسته‌شده بدون تسویه (ClosedWithoutSettlement).
/// بدون تغییر CancelledQuantity، بدون تغییر DeliveryStatus و بدون ارسال restock به استرپی.
/// </summary>
public sealed class ClosedParcelRefundService(
    IClosedParcelRefundStore store,
    INoorCreditService noorCredit)
{
    public async Task<Result<ClosedParcelRefundResponse>> RefundAsync(
        Guid adminId, Guid purchaseId, Guid operationId, string reason, CancellationToken ct)
    {
        var owner = await store.FindOwnerAsync(purchaseId, ct);
        if (owner is null)
        {
            return Result<ClosedParcelRefundResponse>.NotFound(
                PhysicalCancellationErrors.NotFound, ClosedParcelRefundErrorCodes.NotFound);
        }

        var normalizedReason = reason.Trim();
        var admin = new UserId(adminId);

        return await store.ExecuteAsync(owner.BuyerId, owner.CartId, async () =>
        {
            var existingByOperation = await store.FindByOperationIdAsync(operationId, ct);
            if (existingByOperation is not null)
            {
                var sameOperation =
                    existingByOperation.PurchaseId.Value == purchaseId &&
                    existingByOperation.PerformedByUserId == admin &&
                    string.Equals(existingByOperation.Reason, normalizedReason, StringComparison.Ordinal);

                return sameOperation
                    ? Result<ClosedParcelRefundResponse>.Success(ToResponse(existingByOperation))
                    : Conflict(PhysicalCancellationErrors.ClosedParcelRefundConflict, ClosedParcelRefundErrorCodes.Conflict);
            }

            if (await store.IsOperationIdUsedElsewhereAsync(operationId, ct))
            {
                return Conflict(PhysicalCancellationErrors.ClosedParcelRefundConflict, ClosedParcelRefundErrorCodes.Conflict);
            }

            var purchase = await store.FindPurchaseAsync(purchaseId, ct);
            if (purchase is null || purchase.ScopeId != FinancialScope.TarheElahi)
            {
                return Result<ClosedParcelRefundResponse>.NotFound(
                    PhysicalCancellationErrors.NotFound, ClosedParcelRefundErrorCodes.NotFound);
            }

            if (purchase.HasClosedParcelRefund || await store.FindByPurchaseIdAsync(purchaseId, ct) is not null)
            {
                return Conflict(
                    PhysicalCancellationErrors.ClosedParcelRefundAlreadyRefunded,
                    ClosedParcelRefundErrorCodes.AlreadyRefunded);
            }

            if (!purchase.IsClosedWithoutSettlementEligible())
            {
                return Conflict(
                    PhysicalCancellationErrors.ClosedParcelRefundNotEligible,
                    ClosedParcelRefundErrorCodes.NotEligible);
            }

            var remainingShares = purchase.RemainingRefundableShares();
            if (remainingShares is null)
            {
                return Conflict(
                    PhysicalCancellationErrors.ManualReview,
                    ClosedParcelRefundErrorCodes.ManualReview);
            }

            var remaining = remainingShares.Value;
            if (remaining.TotalInNoor <= 0)
            {
                return Conflict(
                    PhysicalCancellationErrors.ClosedParcelRefundNoRemainingAmount,
                    ClosedParcelRefundErrorCodes.NoRemainingAmount);
            }

            if (await store.IsOrderSettledAsync(purchase.CartId, purchase.Id.Value, ct))
            {
                return Conflict(
                    PhysicalCancellationErrors.ClosedParcelRefundAlreadySettled,
                    ClosedParcelRefundErrorCodes.AlreadySettled);
            }

            var now = DateTime.UtcNow;
            purchase.RecordClosedParcelRefund(remaining, now);
            var row = ClosedParcelRefund.Create(operationId, purchase, remaining, admin, normalizedReason, now);
            store.Add(row);

            await CreditAsync(
                purchase.BuyerId, purchase.ScopeId, remaining, operationId,
                purchase.Id.ToString(), admin, normalizedReason, ct);

            return Result<ClosedParcelRefundResponse>.Success(ToResponse(row));
        }, ct);
    }

    private async Task CreditAsync(
        UserId buyer,
        string scopeId,
        PaymentShares refund,
        Guid operationId,
        string referenceId,
        UserId performedBy,
        string reason,
        CancellationToken ct)
    {
        if (refund.RegularInNoor > 0)
        {
            await noorCredit.CreditAsync(new NoorCreditRequest(
                scopeId, buyer, refund.RegularInNoor, LedgerBalanceType.Regular, LedgerReferenceType.Refund,
                referenceId, performedBy, reason, operationId), ct);
        }

        if (refund.GiftInNoor > 0)
        {
            await noorCredit.CreditAsync(new NoorCreditRequest(
                scopeId, buyer, refund.GiftInNoor, LedgerBalanceType.Gift, LedgerReferenceType.Refund,
                referenceId, performedBy, reason, operationId), ct);
        }
    }

    private static Result<ClosedParcelRefundResponse> Conflict(string message, string code) =>
        Result<ClosedParcelRefundResponse>.Conflict(message, code);

    private static ClosedParcelRefundResponse ToResponse(ClosedParcelRefund row) =>
        new(
            row.OperationId,
            row.CartId,
            row.PurchaseId.Value,
            row.RegularRefundInNoor,
            row.GiftRefundInNoor,
            row.RegularRefundInNoor + row.GiftRefundInNoor,
            row.Reason,
            row.CreatedAtUtc);
}
