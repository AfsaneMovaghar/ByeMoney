using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminCourseOrder;

public sealed class GetAdminCourseOrderQueryHandler(ICourseOrderReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetAdminCourseOrderQuery, AdminCourseOrderDetails>
{
    public async Task<AdminCourseOrderDetails> Handle(GetAdminCourseOrderQuery request, CancellationToken ct)
    {
        _ = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        var snapshot = await reader.ReadAsync(null, null, ct);
        var row = CourseOrderMapping.Build(snapshot)
            .SingleOrDefault(item => item.Details.OrderId == request.OrderId)
            ?? throw new NotFoundException(CourseOrderErrors.NotFound);
        var buyer = snapshot.Buyers.Single(item => item.Id == row.BuyerId);

        return new AdminCourseOrderDetails(
            buyer.ExternalUserId,
            string.IsNullOrWhiteSpace(buyer.DisplayName) ? null : buyer.DisplayName,
            row.Details);
    }
}
