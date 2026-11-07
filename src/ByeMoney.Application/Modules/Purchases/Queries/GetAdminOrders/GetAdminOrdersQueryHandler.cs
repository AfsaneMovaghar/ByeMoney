using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using MediatR;
using ByeMoney.Application.Modules.Purchases.Orders.Services;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminOrders;

public sealed class GetAdminOrdersQueryHandler(IOrderReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetAdminOrdersQuery, PurchasePage<AdminOrderListItem>>
{
    private Guid BuyerId => currentUser.UserId
        ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public async Task<PurchasePage<AdminOrderListItem>> Handle(GetAdminOrdersQuery request, CancellationToken ct)
    {
        _ = BuyerId;
        var snapshot = await reader.ReadAsync(null, request.Filter.BuyerExternalUserId, ct);
        var buyers = snapshot.Buyers.ToDictionary(buyer => buyer.Id);
        var page = OrderPaging.Page(OrderMapping.Build(snapshot), request.Filter.ToFilter());
        var refundsByCart = OrderRefunds.ByCart(snapshot);
        var items = page.Items.Select(row =>
        {
            var order = row.Details;
            var buyer = buyers[row.BuyerId];
            var refunded = OrderRefunds.For(order, refundsByCart);

            return new AdminOrderListItem(order.OrderId, order.SourceType, order.CartId,
                order.CreatedAtUtc, order.UpdatedAtUtc, order.FinancialStatus, order.DeliveryStatus,
                order.FinalAmountInNoor, order.ItemCount, order.TopUps.Count, buyer.ExternalUserId,
                string.IsNullOrWhiteSpace(buyer.DisplayName) ? null : buyer.DisplayName, refunded);
        }).ToArray();
        return new PurchasePage<AdminOrderListItem>(items, page.Page, page.PageSize, page.TotalCount);
    }
}
