using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using MediatR;
using ByeMoney.Application.Modules.Purchases.Orders.Services;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyCourseOrders;

public sealed class GetMyCourseOrdersQueryHandler(ICourseOrderReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetMyCourseOrdersQuery, PurchasePage<CourseOrderListItem>>
{
    private Guid BuyerId => currentUser.UserId
        ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public async Task<PurchasePage<CourseOrderListItem>> Handle(GetMyCourseOrdersQuery request, CancellationToken ct)
    {
        var snapshot = await reader.ReadAsync(BuyerId, null, ct);
        var page = CourseOrderPaging.Page(CourseOrderMapping.Build(snapshot), request.Filter);
        return new PurchasePage<CourseOrderListItem>(page.Items.Select(row => row.Details.ToListItem()).ToArray(),
            page.Page, page.PageSize, page.TotalCount);
    }
}
