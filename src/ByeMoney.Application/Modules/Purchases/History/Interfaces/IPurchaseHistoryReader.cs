using ByeMoney.Application.Modules.Purchases.History.DTOs;

namespace ByeMoney.Application.Modules.Purchases.History.Interfaces;

public interface IPurchaseHistoryReader
{
    Task<PurchasePage<PurchaseReadRow>> ListAsync(
        Guid? buyerId, PurchaseHistoryFilter filter, CancellationToken ct);

    Task<PurchaseReceiptData?> GetReceiptAsync(Guid purchaseId, Guid buyerId, CancellationToken ct);

    Task<PurchaseReceiptData?> GetAdminReceiptAsync(Guid purchaseId, CancellationToken ct);
}
