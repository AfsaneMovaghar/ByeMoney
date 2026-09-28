using ByeMoney.API.Contracts.Courses;
using ByeMoney.Application.Modules.Purchases.Commands.AdminPurchaseCourses;
using ByeMoney.Domain.Common;
using ByeMoney.Domain.Modules.Identity.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/admin/courses")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminCoursesController(ISender sender) : ControllerBase
{
    /// <summary>
    /// خرید یا ثبت رایگان یک یا چند دوره به‌نیابت از کاربر.
    /// </summary>
    [HttpPost("purchase")]
    [ProducesResponseType(typeof(PurchaseCoursesApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PurchaseCourses(
        [FromBody] AdminPurchaseCoursesApiRequest request,
        CancellationToken ct)
    {
        var result = await sender.Send(new AdminPurchaseCoursesCommand(
            request.BeneficiaryExternalUserId, request.ExternalCourseIds,
            request.IsFree, request.FreeReason), ct);

        if (result.IsFailure)
        {
            return result.Status switch
            {
                ResultStatus.NotFound => NotFound(new { error = result.ErrorMessage }),
                ResultStatus.Conflict => Conflict(new { error = result.ErrorMessage }),
                _ => BadRequest(new { error = result.ErrorMessage })
            };
        }

        return Ok(PurchaseCoursesApiResponse.From(result.Value));
    }
}
