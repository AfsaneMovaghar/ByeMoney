namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record AdminOrderListItem(
    string OrderId, string SourceType, Guid? CartId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    string FinancialStatus, string DeliveryStatus, decimal? FinalAmountInNoor, int ItemCount,
    int TopUpAttemptCount, string BuyerExternalUserId, string? BuyerDisplayName,
    decimal RefundedAmountInNoor);
