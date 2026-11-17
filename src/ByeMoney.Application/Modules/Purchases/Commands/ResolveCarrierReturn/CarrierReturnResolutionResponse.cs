namespace ByeMoney.Application.Modules.Purchases.Commands.ResolveCarrierReturn;

public sealed record CarrierReturnResolutionResponse(
    Guid PurchaseId,
    string DeliveryStatus,
    DateTime? DeliveredAtUtc,
    DateTime? CarrierReturnedAtUtc,
    string CarrierReturnResolution,
    string CarrierReturnResolutionNote,
    DateTime CarrierReturnResolvedAtUtc,
    Guid CarrierReturnResolvedByUserId);

