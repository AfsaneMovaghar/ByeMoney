using System.Security.Claims;
using System.Text.Json;
using ByeMoney.API.Controllers;
using ByeMoney.Application.Modules.Identity.Authorization;
using ByeMoney.Application.Modules.Identity.Queries.GetUserPermissions;
using ByeMoney.Domain.Modules.Identity.Constants;
using ByeMoney.Domain.Modules.Identity.Permissions;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ByeMoney.UnitTests;

public class UserPermissionsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handler_ReturnsAssignedRbacAndCompatibleFlags(bool canReview)
    {
        var assigned = new List<string>
        {
            Permissions.Courses.Manage, Permissions.Noor.Inject, Permissions.Wallet.View
        };
        if (canReview)
            assigned.Add(Permissions.TopUp.Review);
        var service = new Mock<IUserAuthorizationService>();
        using var cancellation = new CancellationTokenSource();
        service.Setup(x => x.GetPermissionsByExternalUserIdAsync("document-id", cancellation.Token))
            .ReturnsAsync(new UserPermissionsDto { Permissions = assigned, Roles = ["Operator"] });

        var response = await new GetUserPermissionsQueryHandler(service.Object)
            .Handle(new GetUserPermissionsQuery("document-id"), cancellation.Token);

        response.Should().NotBeNull();
        response!.Permissions.Should().BeEquivalentTo(assigned);
        response.Roles.Should().Equal("Operator");
        response.CanReviewTopUps.Should().Be(canReview);
        response.CanAssistTopUp.Should().Be(canReview);
        service.VerifyAll();

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        json.RootElement.GetProperty("permissions").GetArrayLength().Should().Be(assigned.Count);
        json.RootElement.GetProperty("roles")[0].GetString().Should().Be("Operator");
        json.RootElement.GetProperty("canReviewTopUps").GetBoolean().Should().Be(canReview);
        json.RootElement.GetProperty("canAssistTopUp").GetBoolean().Should().Be(canReview);
    }

    [Fact]
    public async Task Handler_UserWithoutRolesReturnsEmptyArraysAndFalseFlags()
    {
        var service = new Mock<IUserAuthorizationService>();
        service.Setup(x => x.GetPermissionsByExternalUserIdAsync("document-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPermissionsDto());

        var response = await new GetUserPermissionsQueryHandler(service.Object)
            .Handle(new GetUserPermissionsQuery("document-id"), CancellationToken.None);

        response.Should().BeEquivalentTo(new UserPermissionsResponse([], [], false, false));
    }

    [Fact]
    public async Task Handler_UnmappedUserReturnsNull()
    {
        var service = new Mock<IUserAuthorizationService>();
        var response = await new GetUserPermissionsQueryHandler(service.Object)
            .Handle(new GetUserPermissionsQuery("unknown"), CancellationToken.None);

        response.Should().BeNull();
    }

    [Fact]
    public async Task Controller_UsesDocumentIdAndReturnsRbacInsteadOfTokenRole()
    {
        var sender = new Mock<ISender>();
        var expected = new UserPermissionsResponse([], [], false, false);
        using var cancellation = new CancellationTokenSource();
        sender.Setup(x => x.Send(It.Is<GetUserPermissionsQuery>(q => q.ExternalUserId == "document-id"), cancellation.Token))
            .ReturnsAsync(expected);
        var controller = CreateController(sender.Object,
            new Claim(AppClaimTypes.DocumentId, "document-id"), new Claim(ClaimTypes.Role, "Admin"));

        var result = await controller.GetPermissions(cancellation.Token);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(expected);
        sender.VerifyAll();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Controller_MissingDocumentIdReturnsUnauthorizedWithoutDispatch(string? documentId)
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var controller = CreateController(sender.Object, new Claim(AppClaimTypes.DocumentId, documentId ?? ""));

        var result = await controller.GetPermissions(CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Controller_UnmappedUserReturnsUnauthorized()
    {
        var sender = new Mock<ISender>();
        var controller = CreateController(sender.Object, new Claim(AppClaimTypes.DocumentId, "unknown"));

        var result = await controller.GetPermissions(CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    private static AdminTopUpController CreateController(ISender sender, params Claim[] claims) => new(sender)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) }
        }
    };
}
