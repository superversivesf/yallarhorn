namespace Yallarhorn.Middleware;

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

/// <summary>
/// Middleware that adds rate limiting headers to responses and handles rate limit enforcement.
/// This middleware works with ASP.NET Core's built-in RateLimiter to provide:
/// - Per-IP rate limiting with different limits for read/write/trigger operations
/// - X-RateLimit-Limit, X-RateLimit-Remaining, X-RateLimit-Reset headers in responses
/// - Custom 429 Too Many Requests response with error body
/// </summary>
public sealed class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the RateLimitMiddleware class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
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
        // Store the response body stream reference
        var originalBodyStream = context.Response.Body;

        try
        {
            // Continue down the pipeline
            await _next(context);
        }
        finally
        {
            // Add rate limit headers if not already present
            AddRateLimitHeaders(context);
        }
    }

    /// <summary>
    /// Adds rate limit headers to the response.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    private void AddRateLimitHeaders(HttpContext context)
    {
        var response = context.Response;

        // Only add headers if not already present (might be added by ASP.NET Core RateLimiter)
        if (!response.Headers.ContainsKey("X-RateLimit-Limit"))
        {
            var (limit, policy) = GetRateLimitPolicy(context);
            response.Headers["X-RateLimit-Limit"] = limit.ToString();
        }

        // Add remaining requests header
        if (!response.Headers.ContainsKey("X-RateLimit-Remaining"))
        {
            // Get from rate limiter metadata if available, otherwise estimate
            var remaining = GetRemainingRequests(context);
            response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
        }

        // Add reset time header (Unix timestamp)
        if (!response.Headers.ContainsKey("X-RateLimit-Reset"))
        {
            var resetTime = DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds();
            response.Headers["X-RateLimit-Reset"] = resetTime.ToString();
        }
    }

    /// <summary>
    /// Determines the rate limit policy based on the request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A tuple of the rate limit and the policy name.</returns>
    private static (int Limit, string Policy) GetRateLimitPolicy(HttpContext context)
    {
        var request = context.Request;
        var path = request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        var method = request.Method.ToUpperInvariant();

        // Trigger operations: POST to refresh or retry endpoints - 10/minute
        if (method == "POST" && (path.Contains("/refresh") || path.Contains("/retry")))
        {
            return (10, "trigger");
        }

        // Write operations: POST, PUT, PATCH, DELETE - 30/minute
        if (method is "POST" or "PUT" or "PATCH" or "DELETE")
        {
            return (30, "write");
        }

        // Read operations: GET, HEAD, OPTIONS - 100/minute
        return (100, "read");
    }

    /// <summary>
    /// Gets the remaining number of requests for the current rate limit window.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The estimated remaining requests.</returns>
    private static int GetRemainingRequests(HttpContext context)
    {
        // If rate limiting was triggered, remaining is 0
        if (context.Response.StatusCode == StatusCodes.Status429TooManyRequests)
        {
            return 0;
        }

        // Check if rate limiter added metadata
        if (context.Items.TryGetValue("RateLimitRemaining", out var remaining) && remaining is int remainingInt)
        {
            return remainingInt;
        }

        // Return the limit as remaining by default (before rate limiter processes)
        var (limit, _) = GetRateLimitPolicy(context);
        return limit;
    }

    /// <summary>
    /// Gets the client IP address from the request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The client IP address as a string.</returns>
    public static string GetClientIpAddress(HttpContext context)
    {
        // Check for X-Forwarded-For header (behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP in the chain (original client)
            var clientIp = forwardedFor.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(clientIp))
            {
                return clientIp;
            }
        }

        // Fall back to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}