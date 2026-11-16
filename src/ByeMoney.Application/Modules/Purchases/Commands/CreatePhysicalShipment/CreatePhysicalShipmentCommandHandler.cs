using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Commands.UpdatePhysicalDeliveryStatus;
using ByeMoney.Application.Modules.Purchases.Delivery.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.CreatePhysicalShipment;

/// <summary>
/// ساخت مرسوله از طریق پورت عمومی <see cref="IShipmentProviderClient"/>؛
/// جزئیات پستکس/واسط دیگر در Infrastructure می‌ماند.
/// </summary>
public sealed class CreatePhysicalShipmentCommandHandler(
    IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId> repository,
    IPhysicalDeliveryStore deliveryStore,
    IEnumerable<IShipmentProviderClient> shipmentClients,
    IUnitOfWork unitOfWork) :
    IRequestHandler<CreatePhysicalShipmentCommand, CreatePhysicalShipmentResponse>
{
    public async Task<CreatePhysicalShipmentResponse> Handle(
        CreatePhysicalShipmentCommand request, CancellationToken ct)
    {
        var owner = await deliveryStore.FindOwnerAsync(request.PurchaseId, ct)
            ?? throw new NotFoundException(OrderErrors.PhysicalPurchaseNotFound);

        var client = ResolveClient(request.Provider);
        if (client is null || !client.IsConfigured)
        {
            throw new DomainException(OrderErrors.ShipmentProviderUnavailable);
        }

        // خواندن آدرس و اعتبارسنجی وضعیت بیرون از فراخوانی شبکه؛ قفل فقط برای MarkShipped.
        var purchase = await repository.GetByIdAsync(new PhysicalProductPurchaseId(request.PurchaseId), ct)
            ?? throw new NotFoundException(OrderErrors.PhysicalPurchaseNotFound);

        if (purchase.DeliveryStatus == PhysicalDeliveryStatus.Shipped &&
            !string.IsNullOrWhiteSpace(purchase.ProviderParcelId) &&
            string.Equals(purchase.TrackingProvider, client.Provider, StringComparison.OrdinalIgnoreCase))
        {
            return new CreatePhysicalShipmentResponse(
                purchase.Id.Value, purchase.DeliveryStatus.ToString(), purchase.ShippedAtUtc,
                purchase.TrackingCode, purchase.TrackingProvider, purchase.ProviderParcelId,
                nameof(ShipmentCreateOutcome.AlreadyExists));
        }

        if (purchase.DeliveryStatus != PhysicalDeliveryStatus.Preparing || purchase.RemainingQuantity == 0)
        {
            throw new DomainException(OrderErrors.ShipmentNotAllowed);
        }

        var address = await deliveryStore.GetCartShippingAddressAsync(owner.CartId, ct);
        if (address is null || !address.IsValid())
        {
            throw new DomainException(OrderErrors.ShipmentAddressRequired);
        }

        var originId = await deliveryStore.GetCartShippingOriginIdAsync(owner.CartId, ct);
        if (string.IsNullOrWhiteSpace(originId))
        {
            throw new DomainException(OrderErrors.ShipmentOriginRequired);
        }

        var createResult = await client.CreateOrGetParcelAsync(
            BuildRequest(purchase, address, originId), ct);

        if (createResult.Outcome is ShipmentCreateOutcome.Failed or ShipmentCreateOutcome.NotConfigured ||
            string.IsNullOrWhiteSpace(createResult.ProviderParcelId))
        {
            throw new DomainException(
                string.IsNullOrWhiteSpace(createResult.ErrorMessage)
                    ? OrderErrors.ShipmentCreateFailed
                    : createResult.ErrorMessage);
        }

        var delivery = await deliveryStore.ExecuteLockedAsync(owner.BuyerId, owner.CartId, async () =>
        {
            var locked = await repository.GetByIdAsync(new PhysicalProductPurchaseId(request.PurchaseId), ct)
                ?? throw new NotFoundException(OrderErrors.PhysicalPurchaseNotFound);

            if (locked.DeliveryStatus == PhysicalDeliveryStatus.Delivered)
            {
                throw new DomainException(OrderErrors.ShipmentNotAllowed);
            }

            locked.MarkShipped(
                createResult.TrackingCode,
                client.Provider,
                createResult.ProviderParcelId);
            repository.Update(locked);
            await unitOfWork.SaveChangesAsync(ct);

            return new PhysicalDeliveryStatusResponse(
                locked.Id.Value, locked.DeliveryStatus.ToString(), locked.ShippedAtUtc, locked.DeliveredAtUtc,
                locked.TrackingCode, locked.TrackingProvider);
        }, ct);

        var warning = createResult.Outcome == ShipmentCreateOutcome.PendingPayment
            ? createResult.ErrorMessage
            : null;

        return CreatePhysicalShipmentResponse.From(
            delivery, createResult.ProviderParcelId, createResult.Outcome.ToString(), warning);
    }

    private IShipmentProviderClient? ResolveClient(string? provider)
    {
        var configured = shipmentClients
            .Where(item => item.IsConfigured && !string.IsNullOrWhiteSpace(item.Provider))
            .GroupBy(item => item.Provider, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(provider))
        {
            return configured.GetValueOrDefault(provider.Trim());
        }

        // پیش‌فرض: تنها واسط پیکربندی‌شده؛ اگر چندتا بود PostEx ترجیح داده می‌شود.
        if (configured.TryGetValue(PhysicalProductPurchase.PostexProvider, out var postex))
        {
            return postex;
        }

        return configured.Values.FirstOrDefault();
    }

    private static ShipmentCreateRequest BuildRequest(
        PhysicalProductPurchase purchase, ShippingAddress address, string originId)
    {
        // کلید یکتا در سطح بای‌مانی؛ پستکس خودش تکرار را مسدود نمی‌کند.
        // وزن و ارزش اظهارشدهٔ صفر = کلاینت واسط از پیش‌فرض تنظیمات خودش استفاده می‌کند.
        var idempotencyKey = $"BM-{purchase.Id.Value:N}";

        return new ShipmentCreateRequest(
            new ShipmentParty(
                address.RecipientName,
                address.RecipientPhone,
                Telephone: address.RecipientPhone,
                NationalCode: null,
                CompanyName: null,
                address.FullAddress,
                address.City,
                address.Province,
                address.PostalCode),
            new ShipmentParcelDetails(
                idempotencyKey,
                purchase.ProductTitle,
                purchase.RemainingQuantity,
                WeightGrams: 0,
                DeclaredValueMinorUnits: 0,
                CurrencyCode: "IRR"),
            Remark: $"ByeMoney purchase {purchase.Id.Value:N}",
            OriginId: originId.Trim());
    }
}
