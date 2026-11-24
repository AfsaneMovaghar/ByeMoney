using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Generic;

namespace ByeMoney.Application.Modules.Purchases.Carts.Services;

/// <summary>
/// تعارض سبد برای یک قلم مشخص که کمبود موجودی نیست. کد <c>CART_CONFLICT</c> و پیام فارسی قلم حفظ می‌شود.
/// </summary>
public interface ICartConflictLineFailure
{
    CartLineError LineError { get; }
}

public sealed class CartConflictLineFailure<T> : Result<T>, ICartConflictLineFailure
{
    private CartConflictLineFailure(string message, CartLineError lineError)
        : base(false, default, message, ResultStatus.Conflict, CartErrorCodes.Conflict)
    {
        LineError = lineError;
    }

    public CartLineError LineError { get; }

    public static CartConflictLineFailure<T> Create(string message, CartLineError lineError) =>
        new(message, lineError);
}

public static class CartFailureCopy
{
    public static Result<T> Copy<T>(Result result)
    {
        if (result is ICartStockFailure stockFailure)
        {
            return CartStockFailure<T>.Create(stockFailure.LineError);
        }

        if (result is ICartConflictLineFailure parcelFailure)
        {
            return CartConflictLineFailure<T>.Create(result.ErrorMessage!, parcelFailure.LineError);
        }

        return result.Status switch
        {
            ResultStatus.NotFound => Result<T>.NotFound(result.ErrorMessage, result.ErrorCode),
            ResultStatus.Conflict => Result<T>.Conflict(result.ErrorMessage!, result.ErrorCode),
            _ => Result<T>.Failure(result.ErrorMessage!, result.ErrorCode)
        };
    }
}
