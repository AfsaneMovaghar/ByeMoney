using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.Carts.Services;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Contracts.Carts;

/// <summary>
/// ترجمهٔ یکسان نتیجهٔ سبد به HTTP برای مسیر خریدار و مسیر ادمین.
/// </summary>
public static class CartHttpResults
{
    public static IActionResult From<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(result.Value);
        }

        var statusCode = result.ErrorCode == CartErrorCodes.Unavailable
            ? StatusCodes.Status503ServiceUnavailable
            : result.Status switch
            {
                ResultStatus.NotFound => StatusCodes.Status404NotFound,
                ResultStatus.Conflict => StatusCodes.Status409Conflict,
                ResultStatus.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest
            };

        var body = new CartErrorResponse(
            result.ErrorCode ?? CartErrorCodes.Invalid,
            result.ErrorMessage!,
            (result as ICartStockFailure)?.LineError ?? (result as ICartConflictLineFailure)?.LineError);

        return new ObjectResult(body) { StatusCode = statusCode };
    }
}
