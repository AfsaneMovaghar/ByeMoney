namespace ByeMoney.Application.Modules.Purchases.Orders.DTOs;

/// <summary>
/// جزئیات سفارش خریدار؛ <c>ShippingAddress</c> فقط برای سبد پرداخت‌شدهٔ دارای کالا پر است.
/// </summary>
public sealed record MyOrderDetails : OrderDetails
{
    public MyOrderDetails(OrderDetails order, MyOrderShippingAddress? shippingAddress)
        : base(order)
    {
        ShippingAddress = shippingAddress;
    }

    public MyOrderShippingAddress? ShippingAddress { get; }
}
