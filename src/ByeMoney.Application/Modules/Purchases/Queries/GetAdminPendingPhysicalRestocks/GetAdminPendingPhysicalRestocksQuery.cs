using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminPendingPhysicalRestocks;

public sealed record GetAdminPendingPhysicalRestocksQuery : IRequest<IReadOnlyList<PendingPhysicalRestockResponse>>;
