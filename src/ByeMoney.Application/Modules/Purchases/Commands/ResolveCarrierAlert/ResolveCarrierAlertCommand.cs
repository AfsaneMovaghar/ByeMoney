using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.ResolveCarrierAlert;

public sealed record ResolveCarrierAlertCommand(
    Guid PurchaseId,
    string? Resolution,
    string? Note) : IRequest<CarrierAlertResolutionResponse>;
