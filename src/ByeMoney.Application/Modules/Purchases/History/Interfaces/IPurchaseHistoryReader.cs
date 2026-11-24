using ByeMoney.Application.Modules.Purchases.History.DTOs;

namespace ByeMoney.Application.Modules.Purchases.History.Interfaces;

public interface IPurchaseHistoryReader
{
    Task<PurchasePage<PurchaseReadRow>> ListAsync(
        Guid? buyerId, PurchaseHistoryFilter filter, string scopeId, CancellationToken ct);

    Task<PurchaseReceiptData?> GetReceiptAsync(Guid purchaseId, Guid buyerId, string scopeId, CancellationToken ct);

    Task<PurchaseReceiptData?> GetAdminReceiptAsync(Guid purchaseId, string scopeId, CancellationToken ct);
}
