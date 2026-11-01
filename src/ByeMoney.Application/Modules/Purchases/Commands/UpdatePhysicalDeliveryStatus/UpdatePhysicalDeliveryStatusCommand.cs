using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.UpdatePhysicalDeliveryStatus;

public sealed record UpdatePhysicalDeliveryStatusCommand(Guid PurchaseId, string Status) :
    IRequest<PhysicalDeliveryStatusResponse>;

public sealed record PhysicalDeliveryStatusResponse(
    Guid PurchaseId, string DeliveryStatus, DateTime? ShippedAtUtc, DateTime? DeliveredAtUtc);
