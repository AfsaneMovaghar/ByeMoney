namespace ByeMoney.Application.Modules.Purchases.Cart.DTOs;

public sealed record CartTransitionRequest(Guid CartId, string BuyerExternalUserId, IReadOnlyList<Guid>? PurchaseIds = null);
