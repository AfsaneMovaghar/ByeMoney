using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.ResolveCarrierReturn;

public sealed record ResolveCarrierReturnCommand(
    Guid PurchaseId,
    string? Resolution,
    string? Note) : IRequest<CarrierReturnResolutionResponse>;

