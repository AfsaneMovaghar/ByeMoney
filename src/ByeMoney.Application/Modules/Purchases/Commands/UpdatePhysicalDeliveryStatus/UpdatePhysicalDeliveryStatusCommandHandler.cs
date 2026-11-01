using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.UpdatePhysicalDeliveryStatus;

public sealed class UpdatePhysicalDeliveryStatusCommandHandler(
    IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId> repository,
    IUnitOfWork unitOfWork) :
    IRequestHandler<UpdatePhysicalDeliveryStatusCommand, PhysicalDeliveryStatusResponse>
{
    public async Task<PhysicalDeliveryStatusResponse> Handle(
        UpdatePhysicalDeliveryStatusCommand request, CancellationToken ct)
    {
        var purchase = await repository.GetByIdAsync(new PhysicalProductPurchaseId(request.PurchaseId), ct)
            ?? throw new NotFoundException(CourseOrderErrors.PhysicalPurchaseNotFound);

        if (request.Status == nameof(PhysicalDeliveryStatus.Shipped))
        {
            purchase.MarkShipped();
        }
        else
        {
            purchase.MarkDelivered();
        }

        repository.Update(purchase);
        await unitOfWork.SaveChangesAsync(ct);

        return new PhysicalDeliveryStatusResponse(
            purchase.Id.Value, purchase.DeliveryStatus.ToString(), purchase.ShippedAtUtc, purchase.DeliveredAtUtc);
    }
}
