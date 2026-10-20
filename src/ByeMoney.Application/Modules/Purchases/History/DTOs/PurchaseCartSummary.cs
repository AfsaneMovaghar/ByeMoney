namespace ByeMoney.Application.Modules.Purchases.History.DTOs;

public sealed record PurchaseCartSummary(
    decimal OriginalTotalInNoor, decimal ItemDiscountInNoor,
    decimal CouponDiscountInNoor, decimal FinalAmountInNoor);
