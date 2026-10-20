using ByeMoney.Application.Modules.Purchases.History.DTOs;
using ByeMoney.Application.Modules.Purchases.History.Interfaces;
using ByeMoney.Application.Modules.Purchases.History.Services;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyCoursePurchaseReceipt;

public sealed class GetMyCoursePurchaseReceiptQueryHandler(
    IPurchaseHistoryReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetMyCoursePurchaseReceiptQuery, PurchaseReceipt>
{
    private Guid BuyerId => currentUser.UserId
        ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public async Task<PurchaseReceipt> Handle(GetMyCoursePurchaseReceiptQuery request, CancellationToken ct)
    {
        var data = await reader.GetReceiptAsync(request.PurchaseId, BuyerId, ct)
            ?? throw new NotFoundException(PurchaseHistoryErrors.NotFound);
        return PurchaseHistoryMapping.ToReceipt(data);
    }
}
