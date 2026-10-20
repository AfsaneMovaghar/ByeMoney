using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.History.Interfaces;
using ByeMoney.Application.Modules.Purchases.History.Services;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminCoursePurchaseReceipt;

public sealed class GetAdminCoursePurchaseReceiptQueryHandler(
    IPurchaseHistoryReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetAdminCoursePurchaseReceiptQuery, PurchaseReceipt>
{
    private Guid BuyerId => currentUser.UserId
        ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public async Task<PurchaseReceipt> Handle(GetAdminCoursePurchaseReceiptQuery request, CancellationToken ct)
    {
        _ = BuyerId;
        var data = await reader.GetAdminReceiptAsync(request.PurchaseId, ct)
            ?? throw new NotFoundException(PurchaseHistoryErrors.NotFound);
        return PurchaseHistoryMapping.ToReceipt(data);
    }
}
