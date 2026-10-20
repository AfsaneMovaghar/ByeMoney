namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record CourseOrderItem(
    string ExternalCourseId, string? Title, decimal? PriceInNoor, decimal? FinalAmountInNoor,
    Guid? PurchaseId, Guid? TransactionId, DateTime? PurchasedAtUtc,
    string FinancialStatus, string DeliveryStatus);
