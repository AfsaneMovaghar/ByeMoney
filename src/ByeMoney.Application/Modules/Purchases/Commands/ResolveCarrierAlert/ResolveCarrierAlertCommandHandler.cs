using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.ResolveCarrierAlert;

public sealed class ResolveCarrierAlertCommandHandler(
    IRepository<PhysicalProductPurchase, PhysicalProductPurchaseId> repository,
    IPhysicalDeliveryStore deliveryStore,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) :
    IRequestHandler<ResolveCarrierAlertCommand, CarrierAlertResolutionResponse>
{
    public async Task<CarrierAlertResolutionResponse> Handle(
        ResolveCarrierAlertCommand request, CancellationToken ct)
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

            purchase.ResolveCarrierAlert(resolution, note, adminId, DateTime.UtcNow);

            repository.Update(purchase);
            await unitOfWork.SaveChangesAsync(ct);

            return new CarrierAlertResolutionResponse(
                purchase.Id.Value,
                purchase.DeliveryStatus.ToString(),
                purchase.DeliveredAtUtc,
                purchase.CarrierAlertCode,
                purchase.CarrierAlertAtUtc,
                purchase.CarrierAlertResolution!.Value.ToString(),
                purchase.CarrierAlertResolutionNote!,
                purchase.CarrierAlertResolvedAtUtc!.Value,
                purchase.CarrierAlertResolvedByUserId!.Value);
        }, ct);
    }
}
