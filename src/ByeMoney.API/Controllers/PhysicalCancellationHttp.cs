using ByeMoney.API.Contracts.Purchases;
using ByeMoney.Application.Modules.Purchases.PhysicalCancellations.Constants;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Common.Generic;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

/// <summary>ترجمهٔ نتیجهٔ لغو کالای فیزیکی به HTTP بر پایهٔ وضعیت و کد ساختاریافتهٔ خطا.</summary>
internal static class PhysicalCancellationHttp
{
    public static IActionResult Respond<T>(ControllerBase controller, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return controller.Ok(result.Value);
        }

        var statusCode = result.ErrorCode == PhysicalCancellationErrorCodes.Unavailable
            ? StatusCodes.Status503ServiceUnavailable
            : result.Status switch
            {
                ResultStatus.NotFound => StatusCodes.Status404NotFound,
                ResultStatus.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };

        return controller.StatusCode(statusCode, new PhysicalCancellationErrorResponse(
            result.ErrorCode ?? PhysicalCancellationErrorCodes.Conflict,
            result.ErrorMessage!));
    }
}
