namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record CourseOrderReadRow(Guid BuyerId, CourseOrderDetails Details);
