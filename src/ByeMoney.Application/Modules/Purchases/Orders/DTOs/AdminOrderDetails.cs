namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record AdminOrderDetails(
    string BuyerExternalUserId, string? BuyerDisplayName, OrderDetails Order);
