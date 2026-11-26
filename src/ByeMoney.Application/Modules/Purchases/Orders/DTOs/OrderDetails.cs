using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Services;

namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public record OrderDetails(
    string OrderId, string SourceType, Guid? CartId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    string FinancialStatus, string DeliveryStatus, decimal? FinalAmountInNoor, int ItemCount,
    string? CartStatus, DateTime? ExpiresAtUtc, Guid? CurrentTopUpRequestId,
    PurchaseCartSummary? CartSummary, IReadOnlyList<OrderItem> Items,
    IReadOnlyList<OrderTopUp> TopUps, Guid? ReservationId,
    string? CouponCode = null,
    decimal ShippingRegularRefundInNoor = 0m,
    decimal ShippingGiftRefundInNoor = 0m)
{
    public OrderListItem ToListItem() => new(
        OrderId, SourceType, CartId, CreatedAtUtc, UpdatedAtUtc, FinancialStatus,
        DeliveryStatus, FinalAmountInNoor, ItemCount,
        OrderCarrierReturnStatus.For(this));
}
