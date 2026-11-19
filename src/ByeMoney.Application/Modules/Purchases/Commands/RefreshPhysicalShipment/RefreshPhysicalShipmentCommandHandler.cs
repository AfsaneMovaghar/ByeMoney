using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Modules.Purchases.Delivery.Services;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.RefreshPhysicalShipment;

/// <summary>
/// خواندن دوبارهٔ مرسوله با همان کلید یکتای ساخت و ثبت نتیجه. خواندن واسط بیرون از تراکنش و قفل
/// انجام می‌شود و اعمال نتیجه در یک تراکنش کوتاه است. هیچ اثر مالی ندارد.
/// </summary>
public sealed class RefreshPhysicalShipmentCommandHandler(
    IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId> repository,
    IPhysicalDeliveryStore deliveryStore,
    IEnumerable<IShipmentProviderClient> shipmentClients,
    IUnitOfWork unitOfWork) :
    IRequestHandler<RefreshPhysicalShipmentCommand, PhysicalShipmentStateResponse>
{
    public async Task<PhysicalShipmentStateResponse> Handle(
        RefreshPhysicalShipmentCommand request, CancellationToken ct)
    {
        var owner = await deliveryStore.FindOwnerAsync(request.PurchaseId, ct)
            ?? throw new NotFoundException(OrderErrors.PhysicalPurchaseNotFound);

        var purchase = await LoadAsync(request.PurchaseId, ct);
        if (purchase.DeliveryStatus != PhysicalDeliveryStatus.Preparing || !purchase.HasCarrierParcel)
        {
            throw new DomainException(OrderErrors.ShipmentRefreshNotAllowed);
        }

        var client = ShipmentClientSelector.Resolve(shipmentClients, purchase.TrackingProvider);
        if (client is null || !client.IsConfigured)
        {
            throw new DomainException(OrderErrors.ShipmentProviderUnavailable);
        }

        // خواندن واسط بدون تراکنش و قفل.
        var result = await client.GetParcelAsync(IdempotencyKey(purchase), ct);

        return await deliveryStore.ExecuteLockedAsync(owner.BuyerId, owner.CartId, async () =>
        {
            var locked = await LoadAsync(request.PurchaseId, ct);
            if (locked.DeliveryStatus != PhysicalDeliveryStatus.Preparing || !locked.HasCarrierParcel)
            {
                throw new DomainException(OrderErrors.ShipmentRefreshNotAllowed);
            }

            switch (result.Outcome)
            {
                case ShipmentCreateOutcome.NotFound:
                    locked.ClearCarrierParcelReference();
                    await SaveAsync(locked, ct);
                    return State(locked, PhysicalShipmentRefreshOutcome.ParcelNotFound,
                        OrderErrors.ShipmentParcelNotFound);

                case ShipmentCreateOutcome.PendingPayment:
                    if (string.IsNullOrWhiteSpace(result.ProviderParcelId))
                    {
                        return State(locked, PhysicalShipmentRefreshOutcome.ReadFailed,
                            OrderErrors.ShipmentStatusUnavailable);
                    }

                    locked.MarkCarrierPaymentPending(DateTime.UtcNow);
                    await SaveAsync(locked, ct);
                    return State(locked, PhysicalShipmentRefreshOutcome.PaymentPending,
                        OrderErrors.ShipmentPaymentPendingHelp);

                case ShipmentCreateOutcome.Created:
                case ShipmentCreateOutcome.AlreadyExists:
                    return await ApplyAcceptedAsync(locked, client.Provider, result, ct);

                default:
                    // خطای خواندن یا واسط خاموش: چیزی عوض نمی‌شود تا وضعیت از دست نرود.
                    return State(locked, PhysicalShipmentRefreshOutcome.ReadFailed,
                        OrderErrors.ShipmentStatusUnavailable);
            }
        }, ct);
    }

    /// <summary>مرسوله پذیرفته شده است؛ با بارکد واقعی و تعداد هم‌خوان «ارسال‌شده» می‌شود.</summary>
    private async Task<PhysicalShipmentStateResponse> ApplyAcceptedAsync(
        PhysicalProductPurchase purchase, string provider, ShipmentCreateResult result, CancellationToken ct)
    {
        var hasBarcode = PhysicalProductPurchase.IsUsablePostalTrackingCode(result.TrackingCode);
        var quantityMatches = purchase.CarrierParcelQuantity == purchase.RemainingQuantity;

        if (hasBarcode && quantityMatches)
        {
            purchase.MarkShipped(result.TrackingCode, provider, purchase.ProviderParcelId);
            purchase.RememberTrackingUrl(result.TrackingUrl);
            await SaveAsync(purchase, ct);
            return State(purchase, PhysicalShipmentRefreshOutcome.Shipped);
        }

        // ارجاع و تاریخ پذیرش به‌روز می‌شود؛ نشان «در انتظار پرداخت» دیگر معنا ندارد.
        purchase.RememberTrackingUrl(result.TrackingUrl);
        purchase.ClearCarrierPaymentPending();
        await SaveAsync(purchase, ct);

        return hasBarcode
            ? State(purchase, PhysicalShipmentRefreshOutcome.AwaitingTrackingCode,
                OrderErrors.ShipmentQuantityMismatch)
            : State(purchase, PhysicalShipmentRefreshOutcome.AwaitingTrackingCode,
                OrderErrors.ShipmentTrackingCodePending);
    }

    private async Task SaveAsync(PhysicalProductPurchase purchase, CancellationToken ct)
    {
        repository.Update(purchase);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<PhysicalProductPurchase> LoadAsync(Guid purchaseId, CancellationToken ct) =>
        await repository.GetByIdAsync(new PhysicalProductPurchaseId(purchaseId), ct)
            ?? throw new NotFoundException(OrderErrors.PhysicalPurchaseNotFound);

    private static string IdempotencyKey(PhysicalProductPurchase purchase) =>
        $"BM-{purchase.Id.Value:N}";

    private static PhysicalShipmentStateResponse State(
        PhysicalProductPurchase purchase, PhysicalShipmentRefreshOutcome outcome, string? warning = null) =>
        new(purchase.Id.Value,
            purchase.DeliveryStatus.ToString(),
            purchase.TrackingCode,
            purchase.TrackingProvider,
            purchase.ProviderParcelId,
            purchase.CarrierPaymentPendingSinceUtc,
            outcome.ToString(),
            warning);
}
