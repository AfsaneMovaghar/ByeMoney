namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record AdminCourseOrderListItem(
    string OrderId, string SourceType, Guid? CartId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    string FinancialStatus, string DeliveryStatus, decimal? FinalAmountInNoor, int ItemCount,
    string BuyerExternalUserId, string? BuyerDisplayName);
