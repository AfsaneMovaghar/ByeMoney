using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.ResolveCarrierReturn;

public sealed class ResolveCarrierReturnCommandHandler(
    IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId> repository,
    IPhysicalDeliveryStore deliveryStore,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) :
    IRequestHandler<ResolveCarrierReturnCommand, CarrierReturnResolutionResponse>
{
    public async Task<CarrierReturnResolutionResponse> Handle(
        ResolveCarrierReturnCommand request, CancellationToken ct)
    {
        var adminId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        var resolution = Enum.Parse<CarrierReturnResolution>(request.Resolution!, true);
        var note = request.Note!.Trim();

        var owner = await deliveryStore.FindOwnerAsync(request.PurchaseId, ct)
            ?? throw new NotFoundException(OrderErrors.PhysicalPurchaseNotFound);

        return await deliveryStore.ExecuteLockedAsync(owner.BuyerId, owner.CartId, async () =>
        {
            var purchase = await repository.GetByIdAsync(new PhysicalProductPurchaseId(request.PurchaseId), ct)
                ?? throw new NotFoundException(OrderErrors.PhysicalPurchaseNotFound);

            purchase.ResolveCarrierReturn(resolution, note, adminId, DateTime.UtcNow);

            repository.Update(purchase);
            await unitOfWork.SaveChangesAsync(ct);

            return new CarrierReturnResolutionResponse(
                purchase.Id.Value,
                purchase.DeliveryStatus.ToString(),
                purchase.DeliveredAtUtc,
                purchase.CarrierReturnedAtUtc,
                purchase.CarrierReturnResolution!.Value.ToString(),
                purchase.CarrierReturnResolutionNote!,
                purchase.CarrierReturnResolvedAtUtc!.Value,
                purchase.CarrierReturnResolvedByUserId!.Value);
        }, ct);
    }
}

