using System.Net;
using System.Text.Json;
using ByeMoney.Domain.Common.Exceptions;
using FluentValidation;

namespace ByeMoney.API.Middleware;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, title, errors) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                "خطای اعتبارسنجی",
                validationEx.Errors.Select(e => e.ErrorMessage).ToList()
            ),
            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                "یافت نشد",
                new List<string> { notFoundEx.Message }
            ),
            DomainException domainEx => (
                HttpStatusCode.BadRequest,
                "خطای قانون کسب‌وکار",
                new List<string> { domainEx.Message }
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                "خطای داخلی سرور",
                new List<string> { "خطایی غیرمنتظره رخ داد." }
            )
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "خطای مدیریت‌نشده در پردازش درخواست {Path}", context.Request.Path);
        }
        else
        {
            _logger.LogWarning("خطای {StatusCode} در {Path}: {Message}",
                (int)statusCode, context.Request.Path, exception.Message);
        }

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            title,
            status = (int)statusCode,
            errors
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}