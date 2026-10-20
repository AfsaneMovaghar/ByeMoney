using ByeMoney.Application.Modules.Purchases.Orders.DTOs;

namespace ByeMoney.Application.Modules.Purchases.Orders.Interfaces;

public interface ICourseOrderReader
{
    Task<CourseOrderSnapshot> ReadAsync(Guid? buyerId, string? buyerExternalUserId, CancellationToken ct);
}
