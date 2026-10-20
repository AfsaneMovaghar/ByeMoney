namespace ByeMoney.Application.Modules.Purchases.History.DTOs;

public sealed record PurchaseListItem(
    Guid PurchaseId, Guid? CartId, string CourseTitle, decimal FinalAmountInNoor,
    DateTime PurchasedAtUtc, string FinancialStatus, string DeliveryStatus);
