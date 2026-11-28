using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.DTOs;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Interfaces;

public interface IClosedParcelRefundStore
{
    Task<PhysicalPurchaseOwner?> FindOwnerAsync(Guid purchaseId, CancellationToken ct);
    Task<PhysicalProductPurchase?> FindPurchaseAsync(Guid purchaseId, CancellationToken ct);
    Task<ClosedParcelRefund?> FindByOperationIdAsync(Guid operationId, CancellationToken ct);
    Task<ClosedParcelRefund?> FindByPurchaseIdAsync(Guid purchaseId, CancellationToken ct);
    Task<bool> IsOperationIdUsedElsewhereAsync(Guid operationId, CancellationToken ct);
    Task<bool> IsOrderSettledAsync(Guid cartId, Guid purchaseId, CancellationToken ct);
    void Add(ClosedParcelRefund refund);
    Task<Result<T>> ExecuteAsync<T>(Guid buyerId, Guid cartId, Func<Task<Result<T>>> work, CancellationToken ct);
}
