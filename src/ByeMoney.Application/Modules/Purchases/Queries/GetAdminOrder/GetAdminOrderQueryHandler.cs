using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Constants;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Application.Modules.Purchases.Orders.Interfaces;
using ByeMoney.Application.Modules.Purchases.Orders.Services;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common.Exceptions;
using ByeMoney.Domain.Modules.Purchases;
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
        var row = OrderMapping.Build(snapshot, includeAdminResolutionDetails: true)
            .SingleOrDefault(item => item.Details.OrderId == request.OrderId)
            ?? throw new NotFoundException(OrderErrors.NotFound);
        var buyer = snapshot.Buyers.Single(item => item.Id == row.BuyerId);
        var cart = OrderShippingAddressFinder.FindCart(snapshot, row.Details);

        // فراخوانی ادمین همهٔ کاربران را در تصویر می‌خواند، پس ادمین ثبت‌کننده هم در همین فهرست هست.
        var createdByAdmin = cart?.CreatedByAdminUserId is Guid adminUserId
            ? snapshot.Buyers.SingleOrDefault(item => item.Id == adminUserId)?.ExternalUserId
            : null;

        return new AdminOrderDetails(
            buyer.ExternalUserId,
            string.IsNullOrWhiteSpace(buyer.DisplayName) ? null : buyer.DisplayName,
            row.Details,
            MapShippingAddress(cart),
            createdByAdmin,
            cart?.AdminFreeReason,
            MapShippingOrigin(cart));
    }

    private static OrderShippingAddress? MapShippingAddress(Cart? cart)
    {
        var address = cart?.ShippingAddress;
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

    private static ShippingOriginView? MapShippingOrigin(Cart? cart)
    {
        if (string.IsNullOrWhiteSpace(cart?.ShippingOriginId))
        {
            return null;
        }

        return new ShippingOriginView(cart.ShippingOriginId, cart.ShippingOriginLabel);
    }
}
