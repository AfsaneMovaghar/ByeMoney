using ByeMoney.API.Contracts.Errors;
using ByeMoney.API.Contracts.Gift;
using ByeMoney.API.Controllers;
using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Application.Modules.Wallet.Commands.AdminGrantGiftNoor;
using ByeMoney.Application.Resources;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ByeMoney.UnitTests;

public sealed class AdminGiftControllerTests
{
    private readonly Mock<ISender> _sender = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private AdminGiftController CreateController()
    {
        _currentUser.Setup(x => x.UserId).Returns(Guid.NewGuid());
        return new AdminGiftController(_sender.Object, _currentUser.Object);
    }

    [Fact]
    public async Task GrantGiftNoor_MissingIdempotencyKey_ReturnsBadRequest()
    {
        var controller = CreateController();
        var request = new AdminGrantGiftNoorApiRequest(["usr-1"], 100m, "هدیه");

        var result = await controller.GrantGiftNoor(null, request, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequest.Value.Should().BeOfType<AdminGiftErrorResponse>().Subject;
        error.Status.Should().Be(StatusCodes.Status400BadRequest);
        error.Errors.Should().Contain(ApplicationErrors.Gift_IdempotencyKeyRequired);
    }

    [Fact]
    public async Task GrantGiftNoor_Success_ReturnsOkWithResponse()
    {
        var controller = CreateController();
        var request = new AdminGrantGiftNoorApiRequest(["usr-1"], 100m, "هدیه");
        var expectedResponse = new AdminGrantGiftNoorResponse(
            Guid.NewGuid(), 100m, 1, 100m, "هدیه", DateTime.UtcNow,
            [new GiftRecipientResponseDto("usr-1", Guid.NewGuid(), "Granted", 100m)]);

        _sender.Setup(x => x.Send(It.IsAny<AdminGrantGiftNoorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminGrantGiftNoorResult.Success(expectedResponse));

        var result = await controller.GrantGiftNoor("valid-key", request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task GrantGiftNoor_InvalidUsers_ReturnsBadRequestWithInvalidUsersList()
    {
        var controller = CreateController();
        var request = new AdminGrantGiftNoorApiRequest(["usr-1", "usr-blocked"], 100m, "هدیه");
        var invalidUsers = new List<InvalidGiftUserDto>
        {
            new("usr-blocked", ApplicationErrors.Gift_UserBlocked)
        };

        _sender.Setup(x => x.Send(It.IsAny<AdminGrantGiftNoorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminGrantGiftNoorResult.WithInvalidUsers(invalidUsers, ApplicationErrors.Gift_HasInvalidUsers));

        var result = await controller.GrantGiftNoor("valid-key", request, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequest.Value.Should().BeOfType<AdminGiftErrorResponse>().Subject;
        error.Status.Should().Be(StatusCodes.Status400BadRequest);
        error.InvalidUsers.Should().BeEquivalentTo(invalidUsers);
    }

    [Fact]
    public async Task GrantGiftNoor_Conflict_ReturnsConflictResult()
    {
        var controller = CreateController();
        var request = new AdminGrantGiftNoorApiRequest(["usr-1"], 100m, "هدیه");

        _sender.Setup(x => x.Send(It.IsAny<AdminGrantGiftNoorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminGrantGiftNoorResult.Conflict(ApplicationErrors.TopUpRequest_IdempotencyConflict));

        var result = await controller.GrantGiftNoor("conflict-key", request, CancellationToken.None);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        var error = conflict.Value.Should().BeOfType<MiddlewareErrorResponse>().Subject;
        error.Status.Should().Be(StatusCodes.Status409Conflict);
        error.Errors.Should().Contain(ApplicationErrors.TopUpRequest_IdempotencyConflict);
    }
}

