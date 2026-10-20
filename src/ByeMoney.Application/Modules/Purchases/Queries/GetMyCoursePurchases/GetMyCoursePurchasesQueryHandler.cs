using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.History.Interfaces;
using ByeMoney.Application.Modules.Purchases.History.Services;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyCoursePurchases;

public sealed class GetMyCoursePurchasesQueryHandler(
    IPurchaseHistoryReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetMyCoursePurchasesQuery, PurchasePage<PurchaseListItem>>
{
    private Guid BuyerId => currentUser.UserId
        ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public async Task<PurchasePage<PurchaseListItem>> Handle(GetMyCoursePurchasesQuery request, CancellationToken ct)
    {
        var page = await reader.ListAsync(BuyerId, new PurchaseHistoryFilter(request.Page, request.PageSize), ct);
        return new PurchasePage<PurchaseListItem>(
            page.Items.Select(PurchaseHistoryMapping.ToListItem).ToArray(), page.Page, page.PageSize, page.TotalCount);
    }
}
