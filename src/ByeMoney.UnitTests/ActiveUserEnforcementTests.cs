using System.Security.Claims;
using ByeMoney.API.Authentication;
using ByeMoney.API.Contracts.Carts;
using ByeMoney.API.Middleware;
using ByeMoney.Application.Modules.Identity.Authorization;
using ByeMoney.Application.Modules.Purchases.Carts.Constants;
using ByeMoney.Application.Modules.Purchases.Carts.DTOs;
using ByeMoney.Domain.Common.Generic;
using ByeMoney.Domain.Modules.Identity.Constants;
using ByeMoney.Domain.Modules.Identity.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ByeMoney.UnitTests;

public sealed class ActiveUserEnforcementTests
{
    [Fact]
    public async Task RbacClaimsTransformation_WhenUserIsInactive_AddsIsActiveFalseAndOmitsPermissions()
    {
        var authService = new Mock<IUserAuthorizationService>();
        authService
            .Setup(x => x.GetPermissionsByExternalUserIdAsync("doc-blocked", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPermissionsDto
            {
                UserId = UserId.New(),
                IsActive = false,
                Roles = ["Admin"],
                Permissions = ["noor.gift"]
            });

        var transformation = new RbacClaimsTransformation(authService.Object);
        var identity = new ClaimsIdentity([new Claim(AppClaimTypes.DocumentId, "doc-blocked")], "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var transformed = await transformation.TransformAsync(principal);

        transformed.HasClaim(AppClaimTypes.IsActive, "false").Should().BeTrue();
        transformed.HasClaim(c => c.Type == AppClaimTypes.Permission).Should().BeFalse();
        transformed.HasClaim(c => c.Type == ClaimTypes.Role).Should().BeFalse();
    }

    [Fact]
    public async Task RbacClaimsTransformation_WhenUserIsActive_AddsIsActiveTrueAndPermissions()
    {
        var authService = new Mock<IUserAuthorizationService>();
        authService
            .Setup(x => x.GetPermissionsByExternalUserIdAsync("doc-active", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPermissionsDto
            {
                UserId = UserId.New(),
                IsActive = true,
                Roles = ["Admin"],
                Permissions = ["noor.gift"]
            });

        var transformation = new RbacClaimsTransformation(authService.Object);
        var identity = new ClaimsIdentity([new Claim(AppClaimTypes.DocumentId, "doc-active")], "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var transformed = await transformation.TransformAsync(principal);

        transformed.HasClaim(AppClaimTypes.IsActive, "true").Should().BeTrue();
        transformed.HasClaim(AppClaimTypes.Permission, "noor.gift").Should().BeTrue();
        transformed.HasClaim(ClaimTypes.Role, "Admin").Should().BeTrue();
    }

    [Fact]
    public async Task ActiveUserMiddleware_WhenAuthenticatedUserIsInactiveOnAuthorizedEndpoint_Returns403()
    {
        var nextCalled = false;
        var middleware = new ActiveUserMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateHttpContext(
            authenticated: true,
            isActive: false,
            metadata: [new AuthorizeAttribute()]);
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ActiveUserMiddleware_WhenAuthenticatedUserIsActiveOnAuthorizedEndpoint_AllowsRequest()
    {
        var nextCalled = false;
        var middleware = new ActiveUserMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateHttpContext(
            authenticated: true,
            isActive: true,
            metadata: [new AuthorizeAttribute()]);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ActiveUserMiddleware_WhenEndpointIsAllowAnonymous_DoesNotBlockEvenWithInactiveJwt()
    {
        var nextCalled = false;
        var middleware = new ActiveUserMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateHttpContext(
            authenticated: true,
            isActive: false,
            metadata: [new AllowAnonymousAttribute(), new AuthorizeAttribute()]);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ActiveUserMiddleware_WhenUnauthenticatedOnServiceEndpoint_DoesNotBlock()
    {
        var nextCalled = false;
        var middleware = new ActiveUserMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = CreateHttpContext(
            authenticated: false,
            isActive: false,
            metadata: [new AuthorizeAttribute()]);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public void CartHttpResults_WhenResultIsForbidden_Returns403()
    {
        var actionResult = CartHttpResults.From(
            Result<CartResponse>.Forbidden("کاربر مسدود است.", CartErrorCodes.UserInactive));

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    private static DefaultHttpContext CreateHttpContext(bool authenticated, bool isActive, object[] metadata)
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test"));

        if (authenticated)
        {
            var identity = new ClaimsIdentity(
                [
                    new Claim(AppClaimTypes.DocumentId, "doc-1"),
                    new Claim(AppClaimTypes.IsActive, isActive ? "true" : "false")
                ],
                "Bearer");
            context.User = new ClaimsPrincipal(identity);
        }

        return context;
    }
}
