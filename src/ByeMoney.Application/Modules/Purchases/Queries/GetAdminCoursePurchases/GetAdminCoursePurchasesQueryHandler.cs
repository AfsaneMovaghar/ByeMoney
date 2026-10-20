using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.History.Interfaces;
using ByeMoney.Application.Modules.Purchases.History.Services;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminCoursePurchases;

public sealed class GetAdminCoursePurchasesQueryHandler(
    IPurchaseHistoryReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetAdminCoursePurchasesQuery, PurchasePage<AdminPurchaseListItem>>
{
    private Guid BuyerId => currentUser.UserId
        ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public async Task<PurchasePage<AdminPurchaseListItem>> Handle(GetAdminCoursePurchasesQuery request, CancellationToken ct)
    {
        _ = BuyerId;
        var page = await reader.ListAsync(null, request.Filter, ct);
        return new PurchasePage<AdminPurchaseListItem>(
            page.Items.Select(PurchaseHistoryMapping.ToAdminListItem).ToArray(), page.Page, page.PageSize, page.TotalCount);
    }
}
