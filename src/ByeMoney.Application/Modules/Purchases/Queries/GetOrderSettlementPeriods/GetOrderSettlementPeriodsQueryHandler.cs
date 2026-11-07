using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.DTOs;
using ByeMoney.Application.Modules.Purchases.Settlements.Interfaces;
using ByeMoney.Application.Resources;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetOrderSettlementPeriods;

public sealed class GetOrderSettlementPeriodsQueryHandler(
    IOrderSettlementStore store,
    ICurrentUserService currentUser) :
    IRequestHandler<GetOrderSettlementPeriodsQuery, PurchasePage<OrderSettlementPeriodResponse>>
{
    public Task<PurchasePage<OrderSettlementPeriodResponse>> Handle(
        GetOrderSettlementPeriodsQuery request, CancellationToken ct)
    {
        _ = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        return store.GetPeriodsAsync(request.Page, request.PageSize, ct);
    }
}
