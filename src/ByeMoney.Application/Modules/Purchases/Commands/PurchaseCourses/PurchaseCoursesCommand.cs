using ByeMoney.Domain.Common;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourses;

public record PurchaseCoursesCommand(
    Guid BuyerUserId,
    IReadOnlyList<string> ExternalCourseIds,
    bool IsFree = false,
    string? FreeReason = null) : IRequest<Result<PurchaseCoursesResponse>>;

