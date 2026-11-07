namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record OrderReadRow(Guid BuyerId, OrderDetails Details);
