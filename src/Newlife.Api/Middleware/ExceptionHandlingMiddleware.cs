using System.Text.Json;
using FluentValidation;
using Newlife.Application.Common.Exceptions;

namespace Newlife.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                logger.LogError(exception, "Unhandled exception after the response started");
                throw;
            }

            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var traceId = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;

        var (status, code, title, detail, errors) = exception switch
        {
            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                "validation.failed",
                "Validation Failed",
                "One or more validation errors occurred.",
                ToErrorDictionary(validation)),
            BadRequestException badRequest => (
                StatusCodes.Status400BadRequest,
                badRequest.Code,
                "Bad Request",
                badRequest.Message,
                null),
            LockedOutException lockedOut => (
                StatusCodes.Status423Locked,
                lockedOut.Code,
                "Locked Out",
                lockedOut.Message,
                null),
            NotFoundException notFound => (
                StatusCodes.Status404NotFound,
                notFound.Code,
                "Not Found",
                notFound.Message,
                null),
            ConflictException conflict => (
                StatusCodes.Status409Conflict,
                conflict.Code,
                "Conflict",
                conflict.Message,
                null),
            ForbiddenException forbidden => (
                StatusCodes.Status403Forbidden,
                forbidden.Code,
                "Forbidden",
                forbidden.Message,
                null),
            UnauthorizedException unauthorized => (
                StatusCodes.Status401Unauthorized,
                unauthorized.Code,
                "Unauthorized",
                unauthorized.Message,
                null),
            _ => (
                StatusCodes.Status500InternalServerError,
                "server.error",
                "Internal Server Error",
                "An unexpected error occurred.",
                null)
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", traceId);
        }
        else
        {
            logger.LogWarning(exception, "Request failed with {StatusCode} {Code}. TraceId: {TraceId}", status, code, traceId);
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.io/{status}",
            ["title"] = title,
            ["status"] = status,
            ["detail"] = detail,
            ["code"] = code,
            ["traceId"] = traceId
        };

        if (errors is not null)
        {
            problem["errors"] = errors;
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }

    private static Dictionary<string, string[]> ToErrorDictionary(ValidationException exception)
        => exception.Errors
            .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
}
