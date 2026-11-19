using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminOrderReceipt;

public sealed class GetAdminOrderReceiptQueryHandler(IOrderReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetAdminOrderReceiptQuery, OrderReceipt>
{
    public async Task<OrderReceipt> Handle(GetAdminOrderReceiptQuery request, CancellationToken ct)
    {
        _ = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        var snapshot = await reader.ReadAsync(null, null, ct);
        var order = OrderMapping.Build(snapshot)
            .SingleOrDefault(row => row.Details.OrderId == request.OrderId)?.Details;

        if (order is null)
        {
            throw new NotFoundException(PurchaseHistoryErrors.NotFound);
        }

        var receipt = OrderReceiptMapping.Build(order, snapshot, includeAdminCarrierFields: true);
        return receipt ?? throw new NotFoundException(PurchaseHistoryErrors.NotFound);
    }
}
