using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Constants;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using MediatR;

namespace ByeMoney.Application.Modules.Purchases.Queries.GetAdminOrder;

public sealed class GetAdminOrderQueryHandler(IOrderReader reader, ICurrentUserService currentUser) :
    IRequestHandler<GetAdminOrderQuery, AdminOrderDetails>
{
    public async Task<AdminOrderDetails> Handle(GetAdminOrderQuery request, CancellationToken ct)
    {
        _ = currentUser.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        var snapshot = await reader.ReadAsync(null, null, ct);
        var row = OrderMapping.Build(snapshot)
            .SingleOrDefault(item => item.Details.OrderId == request.OrderId)
            ?? throw new NotFoundException(OrderErrors.NotFound);
        var buyer = snapshot.Buyers.Single(item => item.Id == row.BuyerId);

        return new AdminOrderDetails(
            buyer.ExternalUserId,
            string.IsNullOrWhiteSpace(buyer.DisplayName) ? null : buyer.DisplayName,
            row.Details,
            FindShippingAddress(snapshot, row.Details));
    }

    private static OrderShippingAddress? FindShippingAddress(OrderSnapshot snapshot, OrderDetails details)
    {
        var address = OrderShippingAddressFinder.FindCart(snapshot, details)?.ShippingAddress;
        return address is null
            ? null
            : new OrderShippingAddress(
                address.Version,
                address.Province,
                address.City,
                address.PostalCode,
                address.FullAddress,
                address.RecipientName,
                address.RecipientPhone);
    }
}
