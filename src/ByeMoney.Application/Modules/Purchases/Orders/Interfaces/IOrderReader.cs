using ByeMoney.Application.Modules.Purchases.Orders.DTOs;

namespace ByeMoney.Application.Modules.Purchases.Orders.Interfaces;

public interface IOrderReader
{
    Task<OrderSnapshot> ReadAsync(Guid? buyerId, string? buyerExternalUserId, string scopeId, CancellationToken ct);
}
