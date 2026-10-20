namespace ByeMoney.Application.Modules.Purchases.History.DTOs;

public sealed record AdminPurchaseListItem(
    Guid PurchaseId, Guid? CartId, string CourseTitle, decimal FinalAmountInNoor,
    DateTime PurchasedAtUtc, string FinancialStatus, string DeliveryStatus,
    string BuyerExternalUserId, string? BuyerDisplayName);
