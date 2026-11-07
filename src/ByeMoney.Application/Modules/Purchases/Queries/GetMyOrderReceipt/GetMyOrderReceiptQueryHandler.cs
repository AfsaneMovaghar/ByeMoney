using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyOrderReceipt;

public sealed class GetMyOrderReceiptQueryHandler(IOrderReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetMyOrderReceiptQuery, OrderReceipt>
{
    private Guid BuyerId => currentUser.UserId
        ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public async Task<OrderReceipt> Handle(GetMyOrderReceiptQuery request, CancellationToken ct)
    {
        var snapshot = await reader.ReadAsync(BuyerId, null, ct);
        var order = OrderMapping.Build(snapshot)
            .SingleOrDefault(row => row.Details.OrderId == request.OrderId)?.Details;

        if (order is null)
        {
            throw new NotFoundException(PurchaseHistoryErrors.NotFound);
        }

        var receipt = OrderReceiptMapping.Build(order, snapshot);
        return receipt ?? throw new NotFoundException(PurchaseHistoryErrors.NotFound);
    }
}
