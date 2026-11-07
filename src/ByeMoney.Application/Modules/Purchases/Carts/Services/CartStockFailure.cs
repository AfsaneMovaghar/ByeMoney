using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Generic;

namespace ByeMoney.Application.Modules.Purchases.Carts.Services;

/// <summary>
/// تعارض سبد به‌دلیل کمبود موجودی یک قلم؛ جزئیات قلم تا پاسخ API حفظ می‌شود.
/// </summary>
public interface ICartStockFailure
{
    CartLineError LineError { get; }
}

public sealed class CartStockFailure<T> : Result<T>, ICartStockFailure
{
    private CartStockFailure(CartLineError lineError)
        : base(false, default, MessageFor(lineError), ResultStatus.Conflict, CartErrorCodes.StockUnavailable)
    {
        LineError = lineError;
    }

    public CartLineError LineError { get; }

    public static CartStockFailure<T> Create(CartLineError lineError) => new(lineError);

    private static string MessageFor(CartLineError lineError)
    {
        if (lineError.Reason == CartStockReasons.InsufficientStock && lineError.AvailableQuantity is int available)
        {
            return CartErrors.InsufficientStock(available);
        }

        return CartErrors.OutOfStock;
    }
}
