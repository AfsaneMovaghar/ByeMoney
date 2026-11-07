namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

public sealed record OrderBuyer(Guid Id, string ExternalUserId, string? DisplayName);
