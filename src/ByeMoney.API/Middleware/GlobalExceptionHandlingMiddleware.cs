using System.Net;
using System.Text.Json;
using ByeMoney.API.Resources;
using ByeMoney.Application.Resources;
using ByeMoney.Application.Modules.TarhElahiIntegration.Exceptions;
using ByeMoney.Domain.Common.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

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
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("درخواست توسط کلاینت لغو شد: {Path}", context.Request.Path);
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499; // Client Closed Request
            }
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
                ApiErrors.Middleware_ValidationErrorTitle,
                validationEx.Errors.Select(e => e.CustomState ?? (object)e.ErrorMessage).ToList()
            ),
            DbUpdateException dbUpdateEx when dbUpdateEx.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505" => (
                HttpStatusCode.Conflict,
                ApiErrors.Middleware_ConflictTitle,
                new List<object> { GetDuplicateConstraintMessage(pgEx.ConstraintName) }
            ),
            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                ApiErrors.Middleware_NotFoundTitle,
                new List<object> { notFoundEx.Message }
            ),
            DomainException domainEx => (
                HttpStatusCode.BadRequest,
                ApiErrors.Middleware_BusinessRuleViolationTitle,
                new List<object> { domainEx.Message }
            ),
            UnauthorizedAccessException unauthorizedEx => (
                HttpStatusCode.Unauthorized,
                ApiErrors.Middleware_UnauthorizedTitle,
                new List<object> { unauthorizedEx.Message }
            ),
            TarhElahiUnavailableException _ => (
                HttpStatusCode.ServiceUnavailable,
                ApiErrors.Middleware_TarhElahiUnavailableTitle,
                new List<object> { ApiErrors.TarhElahi_UnavailableMessage }
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                ApiErrors.Middleware_InternalServerErrorTitle,
                new List<object> { ApiErrors.Middleware_UnexpectedErrorMessage }
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

    private static string GetDuplicateConstraintMessage(string? constraintName) => constraintName switch
    {
        "IX_Users_Phone" => ApplicationErrors.User_PhoneAlreadyExists,
        "IX_Users_ExternalUserId" => ApplicationErrors.User_ExternalUserIdAlreadyExists,
        _ => ApiErrors.Middleware_DuplicateRecordMessage
    };
}