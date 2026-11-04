using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.Carts.Interfaces;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
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
using ByeMoney.Domain.Modules.Wallet.Ledgers;

namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Services;

/// <summary>
/// هماهنگی لغو کالای فیزیکی طبق D09: کاربر فقط کل بخش فیزیکی سبد خودش و ادمین قلم یا تعداد را پیش از ارسال لغو می‌کند.
/// برگشت عادی و هدیه جدا و زیر شناسهٔ تراکنش همان عملیات، با مرجع همان قلم یا سبدِ ثبت خرید (ثبت معکوس D05).
/// </summary>
public sealed class PhysicalCancellationService(
    IPhysicalCancellationStore store,
    ICartStore carts,
    CartService cartService,
    INoorCreditService noorCredit,
    PhysicalRestockService restock)
{
    public async Task<Result<PhysicalCancellationPreviewResponse>> PreviewForBuyerAsync(
        Guid buyerId, Guid cartId, CancellationToken ct)
    {
        var cart = await carts.FindAsync(cartId, buyerId, ct);
        var unpaid = CheckBuyerCart(cart);
        if (unpaid is not null)
        {
            return Copy<PhysicalCancellationPreviewResponse>(unpaid);
        }

        if (cart!.Status != CartStatus.Paid)
        {
            return Result<PhysicalCancellationPreviewResponse>.Success(new PhysicalCancellationPreviewResponse(
                cartId, nameof(PhysicalCancellationOutcome.ReservationReleased), [], 0m, 0m, 0m, []));
        }

        var purchases = await store.GetPurchasesAsync(cartId, ct);
        var plan = await PlanAsync(cart, purchases, BuyerTargets(purchases), ct);
        return Preview(cartId, plan);
    }

    public async Task<Result<PhysicalCancellationPreviewResponse>> PreviewForAdminAsync(
        Guid purchaseId, int quantity, CancellationToken ct)
    {
        var owner = await store.FindOwnerAsync(purchaseId, ct);
        if (owner is null)
        {
            return NotFound<PhysicalCancellationPreviewResponse>();
        }

        var cart = await store.FindCartAsync(owner.CartId, ct);
        var purchases = await store.GetPurchasesAsync(owner.CartId, ct);
        var plan = await PlanAsync(cart!, purchases, AdminTargets(purchases, purchaseId, quantity), ct);
        return Preview(owner.CartId, plan);
    }

    public async Task<Result<PhysicalCancellationResponse>> CancelForBuyerAsync(
        Guid buyerId, Guid cartId, Guid operationId, string reason, CancellationToken ct)
    {
        var cart = await carts.FindAsync(cartId, buyerId, ct);
        var invalid = CheckBuyerCart(cart);
        if (invalid is not null)
        {
            return Copy<PhysicalCancellationResponse>(invalid);
        }

        if (cart!.Status != CartStatus.Paid)
        {
            // پرداخت ناتمام: فقط رزرو طبق I09-C آزاد می‌شود؛ برگشتی وجود ندارد.
            var released = await cartService.CancelAsync(buyerId, cartId, ct);
            if (released.IsFailure)
            {
                return released.ErrorCode == CartErrorCodes.Unavailable
                    ? Result<PhysicalCancellationResponse>.Failure(
                        PhysicalCancellationErrors.Unavailable, PhysicalCancellationErrorCodes.Unavailable)
                    : ConflictResult<PhysicalCancellationResponse>();
            }

            if (released.Value.Status != nameof(CartStatus.Paid))
            {
                return Result<PhysicalCancellationResponse>.Success(new PhysicalCancellationResponse(
                    operationId, cartId, nameof(PhysicalCancellationOutcome.ReservationReleased),
                    nameof(PhysicalCancellationActor.Buyer), [], 0m, 0m, 0m, null));
            }
        }

        var result = await store.ExecuteAsync(buyerId, cartId, async () =>
        {
            var existing = await store.GetOperationAsync(operationId, ct);
            if (existing.Count > 0)
            {
                var sameOperation = existing.All(x =>
                    x.CartId == cartId &&
                    x.Actor == PhysicalCancellationActor.Buyer &&
                    x.PerformedByUserId == new UserId(buyerId));
                return await ReplayAsync(existing, sameOperation, ct);
            }

            var paidCart = await store.FindCartAsync(cartId, ct);
            var purchases = await store.GetPurchasesAsync(cartId, ct);
            var plan = await PlanAsync(paidCart!, purchases, BuyerTargets(purchases), ct);
            return await ApplyAsync(
                plan, buyerId, operationId, PhysicalCancellationActor.Buyer, new UserId(buyerId), reason, ct);
        }, ct);

        return await RestockAfterCommitAsync(result, ct);
    }

    public async Task<Result<PhysicalCancellationResponse>> CancelForAdminAsync(
        Guid adminId, Guid purchaseId, int quantity, Guid operationId, string reason, CancellationToken ct)
    {
        var owner = await store.FindOwnerAsync(purchaseId, ct);
        if (owner is null)
        {
            return NotFound<PhysicalCancellationResponse>();
        }

        var result = await store.ExecuteAsync(owner.BuyerId, owner.CartId, async () =>
        {
            var existing = await store.GetOperationAsync(operationId, ct);
            if (existing.Count > 0)
            {
                var sameOperation = existing.Count == 1 &&
                    existing[0].PurchaseId.Value == purchaseId &&
                    existing[0].Quantity == quantity &&
                    existing[0].Actor == PhysicalCancellationActor.Admin &&
                    existing[0].PerformedByUserId == new UserId(adminId);
                return await ReplayAsync(existing, sameOperation, ct);
            }

            var cart = await store.FindCartAsync(owner.CartId, ct);
            var purchases = await store.GetPurchasesAsync(owner.CartId, ct);
            var plan = await PlanAsync(cart!, purchases, AdminTargets(purchases, purchaseId, quantity), ct);
            return await ApplyAsync(
                plan, owner.BuyerId, operationId, PhysicalCancellationActor.Admin, new UserId(adminId), reason, ct);
        }, ct);

        return await RestockAfterCommitAsync(result, ct);
    }

    /// <summary>
    /// پس از commit برگشت نور، و در بازپخش همان عملیات، برگشت موجودی تأییدنشده دوباره امتحان می‌شود.
    /// نتیجهٔ استرپی پاسخ لغو را تغییر نمی‌دهد؛ شکست آن در صف تلاش مجدد می‌ماند.
    /// </summary>
    private async Task<Result<PhysicalCancellationResponse>> RestockAfterCommitAsync(
        Result<PhysicalCancellationResponse> result, CancellationToken ct)
    {
        if (result.IsSuccess)
        {
            await restock.RestockAsync(result.Value.OperationId, ct);
        }

        return result;
    }

    private static Result? CheckBuyerCart(Cart? cart)
    {
        if (cart is null)
        {
            return NotFound<PhysicalCancellationResponse>();
        }

        if (cart.Items.All(x => x.ItemType != ProductItemType.Product))
        {
            return Result.Conflict(PhysicalCancellationErrors.NoPhysicalItems, PhysicalCancellationErrorCodes.Conflict);
        }

        // نتیجهٔ نامعلوم بانکی به‌صورت حدسی آزاد نمی‌شود (I09-C).
        if (cart.Status == CartStatus.AwaitingTopUp)
        {
            return Result.Conflict(PhysicalCancellationErrors.AwaitingTopUp, PhysicalCancellationErrorCodes.Conflict);
        }

        return null;
    }

    private static IReadOnlyList<PhysicalCancellationTarget> BuyerTargets(
        IReadOnlyList<PhysicalProductPurchase> purchases) =>
        purchases
            .Where(x => x.RemainingQuantity > 0)
            .Select(x => new PhysicalCancellationTarget(x, x.RemainingQuantity))
            .ToArray();

    private static IReadOnlyList<PhysicalCancellationTarget> AdminTargets(
        IReadOnlyList<PhysicalProductPurchase> purchases, Guid purchaseId, int quantity) =>
        purchases
            .Where(x => x.Id.Value == purchaseId)
            .Select(x => new PhysicalCancellationTarget(x, quantity))
            .ToArray();

    private async Task<Result<PhysicalCancellationPlan>> PlanAsync(
        Cart cart,
        IReadOnlyList<PhysicalProductPurchase> purchases,
        IReadOnlyList<PhysicalCancellationTarget> targets,
        CancellationToken ct)
    {
        var shippingPaid = cart.ShippingAmountInNoor > 0
            ? await store.GetShippingPaymentAsync(cart.Id, ct)
            : new PaymentShares(0m, 0m);

        return PhysicalCancellationPlanner.Plan(cart, purchases, targets, shippingPaid);
    }

    private async Task<Result<PhysicalCancellationResponse>> ApplyAsync(
        Result<PhysicalCancellationPlan> planResult,
        Guid buyerId,
        Guid operationId,
        PhysicalCancellationActor actor,
        UserId performedBy,
        string reason,
        CancellationToken ct)
    {
        if (planResult.IsFailure)
        {
            return Copy<PhysicalCancellationResponse>(planResult);
        }

        var plan = planResult.Value;
        var buyer = new UserId(buyerId);
        var rows = new List<PhysicalProductCancellation>();

        for (var i = 0; i < plan.Lines.Count; i++)
        {
            var line = plan.Lines[i];
            var isLast = i == plan.Lines.Count - 1;
            var shippingRefund = isLast ? plan.ShippingRefund : new PaymentShares(0m, 0m);

            line.Purchase.RecordCancellation(line.Quantity, line.Refund);
            var row = PhysicalProductCancellation.Create(
                operationId, line.Purchase, line.Quantity, line.Refund, shippingRefund, actor, performedBy, reason);
            store.Add(row);
            rows.Add(row);

            await CreditAsync(buyer, line.Refund, operationId, line.Purchase.Id.ToString(), performedBy, reason, ct);
        }

        await CreditAsync(
            buyer, plan.ShippingRefund, operationId, plan.Lines[0].Purchase.CartId.ToString(), performedBy, reason, ct);

        return Result<PhysicalCancellationResponse>.Success(
            Response(rows, plan.Lines.Select(x => x.Purchase).ToArray()));
    }

    private async Task CreditAsync(
        UserId buyer,
        PaymentShares refund,
        Guid operationId,
        string referenceId,
        UserId performedBy,
        string reason,
        CancellationToken ct)
    {
        // مرجع برگشت همان مرجع ثبت خرید است تا ثبت معکوس به تراکنش اصلی متصل بماند.
        if (refund.RegularInNoor > 0)
        {
            await noorCredit.CreditAsync(new NoorCreditRequest(
                buyer, refund.RegularInNoor, LedgerBalanceType.Regular, LedgerReferenceType.Refund,
                referenceId, performedBy, reason, operationId), ct);
        }

        if (refund.GiftInNoor > 0)
        {
            await noorCredit.CreditAsync(new NoorCreditRequest(
                buyer, refund.GiftInNoor, LedgerBalanceType.Gift, LedgerReferenceType.Refund,
                referenceId, performedBy, reason, operationId), ct);
        }
    }

    private async Task<Result<PhysicalCancellationResponse>> ReplayAsync(
        IReadOnlyList<PhysicalProductCancellation> existing, bool sameOperation, CancellationToken ct)
    {
        if (!sameOperation)
        {
            return ConflictResult<PhysicalCancellationResponse>();
        }

        var purchases = await store.GetPurchasesAsync(existing[0].CartId, ct);
        return Result<PhysicalCancellationResponse>.Success(Response(existing, purchases));
    }

    private static PhysicalCancellationResponse Response(
        IReadOnlyList<PhysicalProductCancellation> rows, IReadOnlyList<PhysicalProductPurchase> purchases)
    {
        var items = rows
            .Select(row =>
            {
                var purchase = purchases.Single(x => x.Id == row.PurchaseId);
                return new PhysicalCancellationItemResponse(
                    purchase.Id.Value, purchase.ExternalProductId, purchase.ProductTitle,
                    row.Quantity, row.RegularRefundInNoor, row.GiftRefundInNoor);
            })
            .ToArray();
        var shippingRegular = rows.Sum(x => x.ShippingRegularRefundInNoor);
        var shippingGift = rows.Sum(x => x.ShippingGiftRefundInNoor);
        var total = items.Sum(x => x.RegularRefundInNoor + x.GiftRefundInNoor) + shippingRegular + shippingGift;

        return new PhysicalCancellationResponse(
            rows[0].OperationId,
            rows[0].CartId,
            nameof(PhysicalCancellationOutcome.Refunded),
            rows[0].Actor.ToString(),
            items,
            shippingRegular,
            shippingGift,
            total,
            rows[0].CreatedAtUtc);
    }

    private static Result<PhysicalCancellationPreviewResponse> Preview(
        Guid cartId, Result<PhysicalCancellationPlan> planResult)
    {
        if (planResult.IsFailure)
        {
            return Copy<PhysicalCancellationPreviewResponse>(planResult);
        }

        var plan = planResult.Value;
        var items = plan.Lines
            .Select(x => new PhysicalCancellationItemResponse(
                x.Purchase.Id.Value, x.Purchase.ExternalProductId, x.Purchase.ProductTitle,
                x.Quantity, x.Refund.RegularInNoor, x.Refund.GiftInNoor))
            .ToArray();
        var total = plan.Lines.Sum(x => x.Refund.TotalInNoor) + plan.ShippingRefund.TotalInNoor;
        var warnings = plan.WarningCodes
            .Select(code => new PhysicalCancellationWarning(code, WarningMessage(code)))
            .ToArray();

        return Result<PhysicalCancellationPreviewResponse>.Success(new PhysicalCancellationPreviewResponse(
            cartId,
            nameof(PhysicalCancellationOutcome.Refunded),
            items,
            plan.ShippingRefund.RegularInNoor,
            plan.ShippingRefund.GiftInNoor,
            total,
            warnings));
    }

    private static string WarningMessage(string code) => code switch
    {
        PhysicalCancellationWarningCodes.CouponRemoved => PhysicalCancellationErrors.WarningCouponRemoved,
        PhysicalCancellationWarningCodes.FreeShippingRemoved => PhysicalCancellationErrors.WarningFreeShippingRemoved,
        _ => PhysicalCancellationErrors.WarningShippingNotRefunded
    };

    private static Result<T> NotFound<T>() =>
        Result<T>.NotFound(PhysicalCancellationErrors.NotFound, PhysicalCancellationErrorCodes.NotFound);

    private static Result<T> ConflictResult<T>() =>
        Result<T>.Conflict(PhysicalCancellationErrors.Conflict, PhysicalCancellationErrorCodes.Conflict);

    private static Result<T> Copy<T>(Result failure) => failure.Status switch
    {
        ResultStatus.NotFound => Result<T>.NotFound(failure.ErrorMessage, failure.ErrorCode),
        ResultStatus.Conflict => Result<T>.Conflict(failure.ErrorMessage!, failure.ErrorCode),
        _ => Result<T>.Failure(failure.ErrorMessage!, failure.ErrorCode)
    };
}
