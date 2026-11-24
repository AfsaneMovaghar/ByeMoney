using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Purchases;
using ByeMoney.Domain.Modules.Wallet;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetMyOrder;

public sealed class GetMyOrderQueryHandler(IOrderReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetMyOrderQuery, MyOrderDetails>
{
    private Guid BuyerId => currentUser.UserId
        ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

    public async Task<MyOrderDetails> Handle(GetMyOrderQuery request, CancellationToken ct)
    {
        var snapshot = await reader.ReadAsync(BuyerId, null, FinancialScope.TarheElahi, ct);
        var details = OrderMapping.Build(snapshot).SingleOrDefault(row => row.Details.OrderId == request.OrderId)?.Details
            ?? throw new NotFoundException(OrderErrors.NotFound);
        details = OrderMapping.WithoutCarrierAdminFields(details);

        var cart = OrderShippingAddressFinder.FindCart(snapshot, details);
        var paid = cart?.Status == CartStatus.Paid ? cart : null;
        return new MyOrderDetails(details, FindShippingAddress(paid), MapShippingOrigin(paid));
    }

    private static MyOrderShippingAddress? FindShippingAddress(Cart? cart)
    {
        var address = cart?.ShippingAddress;
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

    private static ShippingOriginView? MapShippingOrigin(Cart? cart)
    {
        if (string.IsNullOrWhiteSpace(cart?.ShippingOriginId))
        {
            return null;
        }

        return new ShippingOriginView(cart.ShippingOriginId, cart.ShippingOriginLabel);
    }
}
