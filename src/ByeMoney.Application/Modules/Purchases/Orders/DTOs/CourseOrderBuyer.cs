namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record CourseOrderBuyer(Guid Id, string ExternalUserId, string? DisplayName);
