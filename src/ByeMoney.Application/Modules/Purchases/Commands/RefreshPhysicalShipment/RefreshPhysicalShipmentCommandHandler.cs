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

        // کلید ساخت، شناسهٔ سبد است؛ خواندن واسط بدون تراکنش و قفل.
        var result = await client.GetParcelAsync(IdempotencyKey(purchase), ct);

        return await deliveryStore.ExecuteLockedAsync(owner.BuyerId, owner.CartId, async () =>
        {
            var locked = await LoadAsync(request.PurchaseId, ct);
            if (locked.DeliveryStatus != PhysicalDeliveryStatus.Preparing || !locked.HasCarrierParcel)
            {
                throw new DomainException(OrderErrors.ShipmentRefreshNotAllowed);
            }

            var mates = await PreparingMatesAsync(locked, ct);
            switch (result.Outcome)
            {
                case ShipmentCreateOutcome.NotFound:
                    foreach (var mate in mates)
                    {
                        mate.ClearCarrierParcelReference();
                    }

                    await SaveAsync(mates, ct);
                    return State(locked, PhysicalShipmentRefreshOutcome.ParcelNotFound,
                        OrderErrors.ShipmentParcelNotFound);

                case ShipmentCreateOutcome.PendingPayment:
                    if (string.IsNullOrWhiteSpace(result.ProviderParcelId))
                    {
                        return State(locked, PhysicalShipmentRefreshOutcome.ReadFailed,
                            OrderErrors.ShipmentStatusUnavailable);
                    }

                    var now = DateTime.UtcNow;
                    foreach (var mate in mates)
                    {
                        mate.MarkCarrierPaymentPending(now);
                    }

                    await SaveAsync(mates, ct);
                    return State(locked, PhysicalShipmentRefreshOutcome.PaymentPending,
                        OrderErrors.ShipmentPaymentPendingHelp);

                case ShipmentCreateOutcome.Created:
                case ShipmentCreateOutcome.AlreadyExists:
                    return await ApplyAcceptedAsync(locked, mates, client.Provider, result, ct);

                default:
                    return State(locked, PhysicalShipmentRefreshOutcome.ReadFailed,
                        OrderErrors.ShipmentStatusUnavailable);
            }
        }, ct);
    }

    /// <summary>مرسوله پذیرفته شده است؛ هر قلم با بارکد واقعی و تعداد هم‌خوان «ارسال‌شده» می‌شود.</summary>
    private async Task<PhysicalShipmentStateResponse> ApplyAcceptedAsync(
        PhysicalProductPurchase requested,
        IReadOnlyList<PhysicalProductPurchase> mates,
        string provider,
        ShipmentCreateResult result,
        CancellationToken ct)
    {
        foreach (var mate in mates)
        {
            var hasBarcode = PhysicalProductPurchase.IsUsablePostalTrackingCode(result.TrackingCode);
            var quantityMatches = mate.CarrierParcelQuantity == mate.RemainingQuantity;
            if (hasBarcode && quantityMatches)
            {
                mate.MarkShipped(result.TrackingCode, provider, mate.ProviderParcelId);
                mate.RememberTrackingUrl(result.TrackingUrl);
                continue;
            }

            mate.RememberTrackingUrl(result.TrackingUrl);
            mate.ClearCarrierPaymentPending();
        }

        await SaveAsync(mates, ct);

        var requestedHasBarcode = PhysicalProductPurchase.IsUsablePostalTrackingCode(result.TrackingCode);
        var requestedMatches = requested.CarrierParcelQuantity == requested.RemainingQuantity;
        if (requestedHasBarcode && requestedMatches)
        {
            return State(requested, PhysicalShipmentRefreshOutcome.Shipped);
        }

        return requestedHasBarcode
            ? State(requested, PhysicalShipmentRefreshOutcome.AwaitingTrackingCode,
                OrderErrors.ShipmentQuantityMismatch)
            : State(requested, PhysicalShipmentRefreshOutcome.AwaitingTrackingCode,
                OrderErrors.ShipmentTrackingCodePending);
    }

    private async Task<IReadOnlyList<PhysicalProductPurchase>> PreparingMatesAsync(
        PhysicalProductPurchase purchase, CancellationToken ct)
    {
        var mates = (await deliveryStore.GetPurchasesAsync(purchase.CartId, ct))
            .Where(item =>
                item.DeliveryStatus == PhysicalDeliveryStatus.Preparing &&
                string.Equals(item.ProviderParcelId, purchase.ProviderParcelId, StringComparison.Ordinal))
            .ToList();
        if (mates.All(item => item.Id != purchase.Id))
        {
            mates.Add(purchase);
        }

        return mates;
    }

    private async Task SaveAsync(
        IReadOnlyList<PhysicalProductPurchase> purchases, CancellationToken ct)
    {
        foreach (var purchase in purchases)
        {
            repository.Update(purchase);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<PhysicalProductPurchase> LoadAsync(Guid purchaseId, CancellationToken ct) =>
        await repository.GetByIdAsync(new PhysicalProductPurchaseId(purchaseId), ct)
            ?? throw new NotFoundException(OrderErrors.PhysicalPurchaseNotFound);

    private static string IdempotencyKey(PhysicalProductPurchase purchase) =>
        $"BM-{purchase.CartId:N}";

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
