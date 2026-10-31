namespace ByeMoney.Application.Modules.Purchases.Carts.DTOs;

public sealed record CartTransitionRequest(Guid CartId, string BuyerExternalUserId, IReadOnlyList<Guid>? PurchaseIds = null);
