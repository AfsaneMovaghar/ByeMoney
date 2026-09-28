using ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourses;
using ByeMoney.Domain.Common;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.AdminPurchaseCourses;

public record AdminPurchaseCoursesCommand(
    string BeneficiaryExternalUserId,
    IReadOnlyList<string> ExternalCourseIds,
    bool IsFree = false,
    string? FreeReason = null) : IRequest<Result<PurchaseCoursesResponse>>;
