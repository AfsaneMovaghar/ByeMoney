using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyOrder;

public sealed class GetMyOrderQueryHandler(IOrderReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetMyOrderQuery, OrderDetails>
{
    private Guid BuyerId => currentUser.UserId
        ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public async Task<OrderDetails> Handle(GetMyOrderQuery request, CancellationToken ct)
    {
        var snapshot = await reader.ReadAsync(BuyerId, null, ct);
        return OrderMapping.Build(snapshot).SingleOrDefault(row => row.Details.OrderId == request.OrderId)?.Details
            ?? throw new NotFoundException(OrderErrors.NotFound);
    }
}
