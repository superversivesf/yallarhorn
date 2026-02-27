using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Yallarhorn.Middleware;

/// <summary>
/// Middleware that generates and tracks unique request IDs for all requests.
/// The request ID is generated if not provided, added to response headers,
/// and added to the logger scope for correlated logging.
/// </summary>
public sealed class RequestIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestIdMiddleware> _logger;

    /// <summary>
    /// The name of the request ID header.
    /// </summary>
    public const string RequestIdHeader = "X-Request-ID";

    /// <summary>
    /// The key used to store the request ID in HttpContext.Items.
    /// </summary>
    public const string RequestIdItemKey = "RequestId";

    /// <summary>
    /// Initializes a new instance of the RequestIdMiddleware class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public RequestIdMiddleware(RequestDelegate next, ILogger<RequestIdMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        // Get or generate request ID
        var requestId = GetOrGenerateRequestId(context);

        // Store in HttpContext.Items for easy access
        context.Items[RequestIdItemKey] = requestId;

        // Add to response headers
        context.Response.Headers[RequestIdHeader] = requestId;

        // Add to logger scope for correlated logging
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = requestId
        });

        _logger.LogInformation(
            "Request started: {RequestMethod} {RequestPath}",
            context.Request.Method,
            context.Request.Path);

        try
        {
            await _next(context);
        }
        finally
        {
            _logger.LogInformation(
                "Request completed: {RequestMethod} {RequestPath} - {StatusCode}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode);
        }
    }

    /// <summary>
    /// Gets the request ID from headers or generates a new one.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A request ID string.</returns>
    private static string GetOrGenerateRequestId(HttpContext context)
    {
        // Check for existing X-Request-ID header
        if (context.Request.Headers.TryGetValue(RequestIdHeader, out var requestId))
        {
            var id = requestId.ToString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        // Fall back to X-Trace-ID header
        if (context.Request.Headers.TryGetValue("X-Trace-ID", out var traceId))
        {
            var id = traceId.ToString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        // Generate new GUID-based request ID
        return Guid.NewGuid().ToString();
    }
}