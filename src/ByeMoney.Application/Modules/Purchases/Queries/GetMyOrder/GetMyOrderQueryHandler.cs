using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Purchases;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyOrder;

public sealed class GetMyOrderQueryHandler(IOrderReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetMyOrderQuery, MyOrderDetails>
{
    private Guid BuyerId => currentUser.UserId
        ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public async Task<MyOrderDetails> Handle(GetMyOrderQuery request, CancellationToken ct)
    {
        var snapshot = await reader.ReadAsync(BuyerId, null, ct);
        var details = OrderMapping.Build(snapshot).SingleOrDefault(row => row.Details.OrderId == request.OrderId)?.Details
            ?? throw new NotFoundException(OrderErrors.NotFound);

        return new MyOrderDetails(details, FindShippingAddress(snapshot, details));
    }

    private static MyOrderShippingAddress? FindShippingAddress(OrderSnapshot snapshot, OrderDetails details)
    {
        var cart = OrderShippingAddressFinder.FindCart(snapshot, details);
        var address = cart?.Status == CartStatus.Paid ? cart.ShippingAddress : null;
        return address is null
            ? null
            : new MyOrderShippingAddress(
                address.RecipientName,
                address.RecipientPhone,
                address.Province,
                address.City,
                address.FullAddress,
                address.PostalCode);
    }
}
