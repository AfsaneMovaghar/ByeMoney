using ByeMoney.Application.Modules.Purchases.History.DTOs;

namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public record OrderDetails(
    string OrderId, string SourceType, Guid? CartId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    string FinancialStatus, string DeliveryStatus, decimal? FinalAmountInNoor, int ItemCount,
    string? CartStatus, DateTime? ExpiresAtUtc, Guid? CurrentTopUpRequestId,
    PurchaseCartSummary? CartSummary, IReadOnlyList<OrderItem> Items,
    IReadOnlyList<OrderTopUp> TopUps, Guid? ReservationId)
{
    public OrderListItem ToListItem() => new(
        OrderId, SourceType, CartId, CreatedAtUtc, UpdatedAtUtc, FinancialStatus,
        DeliveryStatus, FinalAmountInNoor, ItemCount);
}
