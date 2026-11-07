using ByeMoney.API.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ByeMoney.UnitTests;

public sealed class GatewayResultKeyFilterTests
{
    private const string StrapiKey = "strapi-test-key-0123456789-abcdefghij";
    private const string FrontendKey = "frontend-test-key-0123456789-abcdefgh";
    private const string ShortKey = "short-key-under-32-bytes";

    private readonly Mock<ILogger<GatewayResultKeyFilter>> _logger = new();

    [Fact]
    public void ValidStrapiKey_IsAuthorizedAndCallerIsStrapi()
    {
        var context = Authorize(StrapiKey, strapiKey: StrapiKey, frontendKey: FrontendKey);

        context.Result.Should().BeNull();
        context.HttpContext.Items[GatewayResultKeyFilter.CallerItemKey].Should().Be(GatewayCaller.Strapi);
        VerifyLogged(LogLevel.Information, "Strapi");
    }

    [Fact]
    public void ValidFrontendKey_IsAuthorizedAndCallerIsFrontend()
    {
        var context = Authorize(FrontendKey, strapiKey: StrapiKey, frontendKey: FrontendKey);

        context.Result.Should().BeNull();
        context.HttpContext.Items[GatewayResultKeyFilter.CallerItemKey].Should().Be(GatewayCaller.Frontend);
        VerifyLogged(LogLevel.Information, "Frontend");
    }

    [Fact]
    public void WrongKey_ReturnsUnauthorized()
    {
        var context = Authorize("wrong-key-0123456789-abcdefghijklmnop", strapiKey: StrapiKey, frontendKey: FrontendKey);

        context.Result.Should().BeOfType<UnauthorizedObjectResult>();
        context.HttpContext.Items.Should().NotContainKey(GatewayResultKeyFilter.CallerItemKey);
    }

    [Fact]
    public void MissingKeyHeader_ReturnsUnauthorized()
    {
        var context = Authorize(provided: null, strapiKey: StrapiKey, frontendKey: FrontendKey);

        context.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public void ShortConfiguredKey_IsNeverAccepted()
    {
        var context = Authorize(ShortKey, strapiKey: ShortKey, frontendKey: ShortKey);

        context.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public void FrontendKeyWithoutConfiguredFrontendKey_ReturnsUnauthorized()
    {
        var context = Authorize(FrontendKey, strapiKey: StrapiKey, frontendKey: null);

        context.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public void LegacyByeMoneyServiceKey_IsNoLongerAccepted()
    {
        var legacyKey = "legacy-byemoney-key-0123456789-abcdef";
        var settings = new Dictionary<string, string?>
        {
            ["BYEMONEY_SERVICE_KEY"] = legacyKey
        };

        var context = Authorize(legacyKey, settings);

        context.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public void CallerIsNotReadFromHeaderOrBody()
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Caller"] = "Frontend"
        };

        var context = Authorize(StrapiKey, strapiKey: StrapiKey, frontendKey: FrontendKey, extraHeaders: headers);

        context.HttpContext.Items[GatewayResultKeyFilter.CallerItemKey].Should().Be(GatewayCaller.Strapi);
    }

    private AuthorizationFilterContext Authorize(
        string? provided,
        string? strapiKey,
        string? frontendKey,
        Dictionary<string, string>? extraHeaders = null)
    {
        var settings = new Dictionary<string, string?>
        {
            [GatewayResultKeyFilter.StrapiKeyName] = strapiKey,
            [GatewayResultKeyFilter.FrontendKeyName] = frontendKey
        };

        return Authorize(provided, settings, extraHeaders);
    }

    private AuthorizationFilterContext Authorize(
        string? provided,
        Dictionary<string, string?> settings,
        Dictionary<string, string>? extraHeaders = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var httpContext = new DefaultHttpContext();

        if (provided is not null)
        {
            httpContext.Request.Headers["X-Service-Key"] = provided;
        }

        foreach (var header in extraHeaders ?? [])
        {
            httpContext.Request.Headers[header.Key] = header.Value;
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var context = new AuthorizationFilterContext(actionContext, []);

        new GatewayResultKeyFilter(configuration, _logger.Object).OnAuthorization(context);
        return context;
    }

    private void VerifyLogged(LogLevel level, string expectedCaller)
    {
        _logger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(expectedCaller)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
