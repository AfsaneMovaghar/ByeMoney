using ByeMoney.API.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ByeMoney.UnitTests;

public sealed class RequestCancellationMiddlewareTests
{
    [Fact]
    public async Task AbortedClientRequestIsNotAnInternalServerError()
    {
        var context = new DefaultHttpContext();
        context.RequestAborted = new CancellationToken(true);
        var middleware = new GlobalExceptionHandlingMiddleware(_ => throw new OperationCanceledException(context.RequestAborted),
            NullLogger<GlobalExceptionHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        context.Response.StatusCode.Should().Be(499);
    }
    [Fact]
    public async Task IncompleteRequestBodyIsReportedAsBadRequest()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionHandlingMiddleware(_ => throw new BadHttpRequestException("Unexpected end of request content."),
            NullLogger<GlobalExceptionHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        context.Response.StatusCode.Should().Be(400);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().Contain("\"status\":400").And.NotContain("Unexpected end");
    }
}
