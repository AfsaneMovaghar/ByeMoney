using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.UpdatePhysicalDeliveryStatus;

public sealed class UpdatePhysicalDeliveryStatusCommandHandler(
    IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId> repository,
    IPhysicalDeliveryStore deliveryStore,
    IUnitOfWork unitOfWork) :
    IRequestHandler<UpdatePhysicalDeliveryStatusCommand, PhysicalDeliveryStatusResponse>
{
    public async Task<PhysicalDeliveryStatusResponse> Handle(
        UpdatePhysicalDeliveryStatusCommand request, CancellationToken ct)
    {
        var owner = await deliveryStore.FindOwnerAsync(request.PurchaseId, ct)
            ?? throw new NotFoundException(OrderErrors.PhysicalPurchaseNotFound);

        // خواندن و ذخیره زیر قفل خریدار و سبد انجام می‌شود تا با لغو هم‌زمان ناسازگار نشود.
        return await deliveryStore.ExecuteLockedAsync(owner.BuyerId, owner.CartId, async () =>
        {
            var purchase = await repository.GetByIdAsync(new PhysicalProductPurchaseId(request.PurchaseId), ct)
                ?? throw new NotFoundException(OrderErrors.PhysicalPurchaseNotFound);

            if (request.Status == nameof(PhysicalDeliveryStatus.Shipped))
            {
                purchase.MarkShipped(request.TrackingCode, request.TrackingProvider, request.ProviderParcelId);
            }
            else
            {
                if (purchase.HasUnresolvedCarrierReturn || purchase.HasClosedWithoutSettlementReturn)
                {
                    throw new DomainException(OrderErrors.PhysicalPurchase_DeliveryBlockedByCarrierReturn);
                }

                purchase.MarkDelivered();
            }

            repository.Update(purchase);
            await unitOfWork.SaveChangesAsync(ct);

            return new PhysicalDeliveryStatusResponse(
                purchase.Id.Value, purchase.DeliveryStatus.ToString(), purchase.ShippedAtUtc, purchase.DeliveredAtUtc,
                purchase.TrackingCode, purchase.TrackingProvider);
        }, ct);
    }
}
