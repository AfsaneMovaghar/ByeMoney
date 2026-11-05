using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;
using ByeMoney.Application.Modules.Purchases.Orders.Services;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminCourseOrders;

public sealed class GetAdminCourseOrdersQueryHandler(ICourseOrderReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetAdminCourseOrdersQuery, PurchasePage<AdminCourseOrderListItem>>
{
    private Guid BuyerId => currentUser.UserId
        ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public async Task<PurchasePage<AdminCourseOrderListItem>> Handle(GetAdminCourseOrdersQuery request, CancellationToken ct)
    {
        _ = BuyerId;
        var snapshot = await reader.ReadAsync(null, request.Filter.BuyerExternalUserId, ct);
        var buyers = snapshot.Buyers.ToDictionary(buyer => buyer.Id);
        var page = CourseOrderPaging.Page(CourseOrderMapping.Build(snapshot), request.Filter.ToFilter());
        var refundsByCart = snapshot.PhysicalCancellations
            .GroupBy(cancellation => cancellation.CartId)
            .ToDictionary(group => group.Key, group => group.Sum(TotalRefund));
        var items = page.Items.Select(row =>
        {
            var order = row.Details;
            var buyer = buyers[row.BuyerId];
            var refunded = order.CartId.HasValue && refundsByCart.TryGetValue(order.CartId.Value, out var total)
                ? total
                : 0m;
            if (order.FinalAmountInNoor.HasValue)
            {
                refunded = Math.Min(refunded, order.FinalAmountInNoor.Value);
            }

            return new AdminCourseOrderListItem(order.OrderId, order.SourceType, order.CartId,
                order.CreatedAtUtc, order.UpdatedAtUtc, order.FinancialStatus, order.DeliveryStatus,
                order.FinalAmountInNoor, order.ItemCount, order.TopUps.Count, buyer.ExternalUserId,
                string.IsNullOrWhiteSpace(buyer.DisplayName) ? null : buyer.DisplayName, refunded);
        }).ToArray();
        return new PurchasePage<AdminCourseOrderListItem>(items, page.Page, page.PageSize, page.TotalCount);
    }

    private static decimal TotalRefund(PhysicalProductCancellation cancellation) =>
        cancellation.RegularRefundInNoor + cancellation.GiftRefundInNoor +
        cancellation.ShippingRegularRefundInNoor + cancellation.ShippingGiftRefundInNoor;
}
