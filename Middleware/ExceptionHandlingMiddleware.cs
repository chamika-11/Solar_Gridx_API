// Smart Solar Microgrid Trading System - centralized safe exception responses.
using MongoDB.Driver;
using SolarGridX.Api.Common;
using SolarGridX.Api.DTOs.Responses;

namespace SolarGridX.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    // Converts known application and database conflicts into consistent API errors.
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException exception)
        {
            await WriteAsync(context, exception.StatusCode, exception.Message, exception.Errors);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            await WriteAsync(context, StatusCodes.Status409Conflict, "A record with the supplied unique value already exists.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled request failure for {Path}", context.Request.Path);
            await WriteAsync(context, StatusCodes.Status500InternalServerError, "An unexpected server error occurred.");
        }
    }

    // Writes the standard, non-sensitive error payload.
    private static Task WriteAsync(HttpContext context, int status, string message, Dictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new ErrorResponse(status, message, errors, DateTime.UtcNow));
    }
}
