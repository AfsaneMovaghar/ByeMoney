using ByeMoney.API.Contracts.Courses;
using ByeMoney.Application.Modules.Purchases.Queries.Checkout;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourses;
using ByeMoney.Application.Resources;
using ByeMoney.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByeMoney.API.Controllers;

[ApiController]
[Route("api/courses")]
[Authorize]
public class CoursesController(ISender sender, ICurrentUserService currentUserService) : ControllerBase
{
    private readonly ISender _sender = sender;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    [HttpGet("{externalCourseId}/checkout")]
    [ProducesResponseType(typeof(CourseCheckoutResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CourseCheckoutResponse>> GetCheckout(string externalCourseId, CancellationToken ct)
        => Ok(await _sender.Send(new GetCourseCheckoutQuery(externalCourseId), ct));

    /// <summary>
    /// Purchases one or more TarhElahi courses using the authenticated user's Noor balance.
    /// </summary>
    [HttpPost("purchase")]
    [ProducesResponseType(typeof(PurchaseCoursesApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PurchaseCourses(
        [FromBody] PurchaseCoursesApiRequest request,
        CancellationToken ct)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        var result = await _sender.Send(new PurchaseCoursesCommand(userId, request.ExternalCourseIds), ct);

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
