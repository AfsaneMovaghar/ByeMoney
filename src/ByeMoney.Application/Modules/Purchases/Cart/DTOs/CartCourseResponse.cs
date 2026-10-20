namespace ByeMoney.Application.Modules.Purchases.Cart.DTOs;

public sealed record CartCourseResponse(string ExternalCourseId, string Title, decimal PriceInNoor, string PurchaseStatus);
