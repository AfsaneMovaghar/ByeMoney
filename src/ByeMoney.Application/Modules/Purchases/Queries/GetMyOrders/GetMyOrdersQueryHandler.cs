using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using MediatR;
using ByeMoney.Application.Modules.Purchases.Orders.Services;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyOrders;

public sealed class GetMyOrdersQueryHandler(IOrderReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetMyOrdersQuery, PurchasePage<OrderListItem>>
{
    private Guid BuyerId => currentUser.UserId
        ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public async Task<PurchasePage<OrderListItem>> Handle(GetMyOrdersQuery request, CancellationToken ct)
    {
        var snapshot = await reader.ReadAsync(BuyerId, null, ct);
        var page = OrderPaging.Page(OrderMapping.Build(snapshot), request.Filter);
        return new PurchasePage<OrderListItem>(page.Items.Select(row => row.Details.ToListItem()).ToArray(),
            page.Page, page.PageSize, page.TotalCount);
    }
}
