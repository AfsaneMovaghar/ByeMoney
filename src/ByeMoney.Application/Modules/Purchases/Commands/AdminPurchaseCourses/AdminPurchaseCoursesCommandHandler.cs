using ByeMoney.Application.Modules.Identity.Users.Commands.SyncUserFromStrapi;
using ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourses;
using ByeMoney.Domain.Common;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Commands.AdminPurchaseCourses;

public sealed class AdminPurchaseCoursesCommandHandler(ISender sender)
    : IRequestHandler<AdminPurchaseCoursesCommand, Result<PurchaseCoursesResponse>>
{
    public async Task<Result<PurchaseCoursesResponse>> Handle(AdminPurchaseCoursesCommand request, CancellationToken ct)
    {
        var buyerUserId = await sender.Send(new SyncUserFromStrapiCommand(request.BeneficiaryExternalUserId), ct);
        return await sender.Send(new PurchaseCoursesCommand(
            buyerUserId, request.ExternalCourseIds, request.IsFree, request.FreeReason), ct);
    }
}
