namespace ByeMoney.Application.Modules.Purchases.History.DTOs;

public sealed record PurchasePage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
