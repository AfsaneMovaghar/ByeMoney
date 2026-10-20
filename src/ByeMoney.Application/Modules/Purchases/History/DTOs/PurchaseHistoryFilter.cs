namespace ByeMoney.Application.Modules.Purchases.History.DTOs;

public sealed record PurchaseHistoryFilter(
    int Page = 1, int PageSize = 20, string? BuyerExternalUserId = null,
    DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null,
    string? FinancialStatus = null, string? DeliveryStatus = null);
