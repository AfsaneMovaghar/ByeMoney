namespace ByeMoney.Application.Modules.Purchases.Commands.ResolveCarrierAlert;

public sealed record CarrierAlertResolutionResponse(
    Guid PurchaseId,
    string DeliveryStatus,
    DateTime? DeliveredAtUtc,
    int? CarrierAlertCode,
    DateTime? CarrierAlertAtUtc,
    string CarrierAlertResolution,
    string CarrierAlertResolutionNote,
    DateTime CarrierAlertResolvedAtUtc,
    Guid CarrierAlertResolvedByUserId);
