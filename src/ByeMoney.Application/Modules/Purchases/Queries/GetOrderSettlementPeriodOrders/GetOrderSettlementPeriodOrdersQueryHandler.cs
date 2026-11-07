using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetOrderSettlementPeriodOrders;

public sealed class GetOrderSettlementPeriodOrdersQueryHandler(
    IOrderSettlementStore store,
    ICurrentUserService currentUser) :
    IRequestHandler<GetOrderSettlementPeriodOrdersQuery, PurchasePage<SettlementOrderResponse>>
{
    public async Task<PurchasePage<SettlementOrderResponse>> Handle(
        GetOrderSettlementPeriodOrdersQuery request, CancellationToken ct)
    {
        _ = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        if (!await store.PeriodExistsAsync(request.PeriodId, ct))
        {
            throw new NotFoundException(OrderSettlementErrors.PeriodNotFound);
        }

        return await store.GetPeriodOrdersAsync(request.PeriodId, request.Page, request.PageSize, ct);
    }
}
