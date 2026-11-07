using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.Services;
using ByeMoney.Application.Resources;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetOrderSettlementPreview;

public sealed class GetOrderSettlementPreviewQueryHandler(
    OrderSettlementCandidateService candidates,
    ICurrentUserService currentUser) :
    IRequestHandler<GetOrderSettlementPreviewQuery, OrderSettlementPreviewResponse>
{
    public async Task<OrderSettlementPreviewResponse> Handle(GetOrderSettlementPreviewQuery request, CancellationToken ct)
    {
        _ = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        var orders = await candidates.GetUnsettledAsync(ct);
        var pageItems = orders
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToArray();

        return new OrderSettlementPreviewResponse(
            orders.Count,
            orders.Sum(order => order.RegularNetAmountInNoor),
            orders.Sum(order => order.GiftNetAmountInNoor),
            orders.Sum(order => order.NetAmountInNoor),
            OrderSettlementPreviewToken.Compute(orders),
            new PurchasePage<SettlementOrderResponse>(pageItems, request.Page, request.PageSize, orders.Count));
    }
}
