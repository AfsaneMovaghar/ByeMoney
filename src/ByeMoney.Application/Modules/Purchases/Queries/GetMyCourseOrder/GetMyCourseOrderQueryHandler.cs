using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyCourseOrder;

public sealed class GetMyCourseOrderQueryHandler(ICourseOrderReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetMyCourseOrderQuery, CourseOrderDetails>
{
    private Guid BuyerId => currentUser.UserId
        ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public async Task<CourseOrderDetails> Handle(GetMyCourseOrderQuery request, CancellationToken ct)
    {
        var snapshot = await reader.ReadAsync(BuyerId, null, ct);
        return CourseOrderMapping.Build(snapshot).SingleOrDefault(row => row.Details.OrderId == request.OrderId)?.Details
            ?? throw new NotFoundException(CourseOrderErrors.NotFound);
    }
}
