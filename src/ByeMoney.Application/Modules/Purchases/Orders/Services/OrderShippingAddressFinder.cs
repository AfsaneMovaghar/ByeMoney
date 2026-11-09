using ByeMoney.Application.Modules.Purchases.Orders.Constants;
using ByeMoney.Application.Modules.Purchases.Orders.DTOs;
using ByeMoney.Domain.Modules.Purchases;

namespace ByeMoney.Application.Modules.Purchases.Orders.Services;

public static class OrderShippingAddressFinder
{
    public static Cart? FindCart(OrderSnapshot snapshot, OrderDetails details)
    {
        if (details.SourceType != OrderSourceType.Cart.ToString() || details.CartId is null)
        {
            return null;
        }

        return snapshot.Carts.SingleOrDefault(cart => cart.Id == details.CartId);
    }
}
