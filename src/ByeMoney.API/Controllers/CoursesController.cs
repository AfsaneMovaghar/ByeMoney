using ByeMoney.API.Contracts.Courses;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Purchases.Commands.PurchaseCourse;
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

    /// <summary>
    /// Purchases a TarhElahi course using the authenticated user's Noor balance.
    /// </summary>
    [HttpPost("purchase")]
    [ProducesResponseType(typeof(PurchaseCourseApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PurchaseCourse(
        [FromBody] PurchaseCourseApiRequest request,
        CancellationToken ct)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException(ApplicationErrors.Wallet_UserNotAuthenticated);

        var result = await _sender.Send(new PurchaseCourseCommand(userId, request.ExternalCourseId), ct);

        if (result.IsFailure)
        {
            return result.Status switch
            {
                ResultStatus.NotFound => NotFound(new { error = result.ErrorMessage }),
                ResultStatus.Conflict => Conflict(new { error = result.ErrorMessage }),
                _ => BadRequest(new { error = result.ErrorMessage })
            };
        }

        var response = new PurchaseCourseApiResponse(
            result.Value.PurchaseId,
            result.Value.ExternalCourseId,
            result.Value.CourseTitle,
            result.Value.PriceInNoor,
            result.Value.Status,
            result.Value.PurchasedAtUtc);

        return Ok(response);
    }
}