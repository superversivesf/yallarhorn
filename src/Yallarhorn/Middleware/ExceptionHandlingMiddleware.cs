using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Yallarhorn.Models;

namespace Yallarhorn.Middleware;

/// <summary>
/// Middleware that catches unhandled exceptions and returns standardized error responses.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Initializes a new instance of the ExceptionHandlingMiddleware class.
    /// </summary>
    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        IWebHostEnvironment environment,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
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
        _logger.LogError(exception, "An unhandled exception occurred during request processing");

        var (statusCode, error) = MapExceptionToResponse(exception);
        var requestId = GetRequestId(context);

        var errorDetails = new ErrorDetails
        {
            Status = (int)statusCode,
            Error = error,
            Detail = GetExceptionDetail(exception),
            RequestId = requestId,
            Timestamp = DateTime.UtcNow
        };

        // Only include stack trace in development environment
        if (_environment.IsDevelopment())
        {
            errorDetails.StackTrace = exception.StackTrace;
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(errorDetails, JsonOptions);
        await context.Response.WriteAsync(json);
    }

    private (HttpStatusCode statusCode, string error) MapExceptionToResponse(Exception exception)
    {
        // Note: More specific exception types must come before base types (e.g., ArgumentNullException before ArgumentException)
        return exception switch
        {
            ArgumentNullException => (HttpStatusCode.BadRequest, "Bad Request"),
            ArgumentOutOfRangeException => (HttpStatusCode.BadRequest, "Bad Request"),
            ArgumentException => (HttpStatusCode.BadRequest, "Bad Request"),
            
            KeyNotFoundException => (HttpStatusCode.NotFound, "Not Found"),
            InvalidOperationException => (HttpStatusCode.NotFound, "Not Found"),
            
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
            
            NotSupportedException => (HttpStatusCode.BadRequest, "Bad Request"),
            
            TimeoutException => (HttpStatusCode.RequestTimeout, "Request Timeout"),
            
            FormatException => (HttpStatusCode.BadRequest, "Bad Request"),
            
            _ => (HttpStatusCode.InternalServerError, "Internal Server Error")
        };
    }

    private static string GetExceptionDetail(Exception exception)
    {
        // Note: More specific exception types must come before base types (e.g., ArgumentNullException before ArgumentException)
        return exception switch
        {
            ArgumentNullException argEx => $"Parameter '{argEx.ParamName}' cannot be null.",
            ArgumentOutOfRangeException rangeEx => $"Parameter '{rangeEx.ParamName}' is out of range. {rangeEx.Message}",
            ArgumentException argEx => argEx.Message,
            KeyNotFoundException => "The requested resource was not found.",
            InvalidOperationException opEx => opEx.Message,
            TimeoutException => "The operation timed out.",
            UnauthorizedAccessException => "Access is denied.",
            NotSupportedException notSupEx => notSupEx.Message,
            FormatException fmtEx => fmtEx.Message,
            _ => exception.Message
        };
    }

    private static string GetRequestId(HttpContext context)
    {
        // Try to get request ID from various headers
        if (context.Request.Headers.TryGetValue("X-Request-ID", out var requestId))
        {
            return requestId.ToString();
        }

        if (context.Request.Headers.TryGetValue("X-Trace-ID", out var traceId))
        {
            return traceId.ToString();
        }

        // Use the trace identifier assigned by ASP.NET Core
        return context.TraceIdentifier;
    }
}