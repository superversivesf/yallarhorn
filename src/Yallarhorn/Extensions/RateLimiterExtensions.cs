namespace Yallarhorn.Extensions;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

/// <summary>
/// Extension methods for configuring rate limiting services.
/// </summary>
public static class RateLimiterExtensions
{
    /// <summary>
    /// Rate limit for read operations (GET requests) - 100 requests per minute.
    /// </summary>
    public const int ReadLimit = 100;

    /// <summary>
    /// Rate limit for write operations (POST/PUT/PATCH/DELETE) - 30 requests per minute.
    /// </summary>
    public const int WriteLimit = 30;

    /// <summary>
    /// Rate limit for trigger operations (refresh, retry) - 10 requests per minute.
    /// </summary>
    public const int TriggerLimit = 10;

    /// <summary>
    /// Rate limit window in seconds (1 minute).
    /// </summary>
    public const int WindowSeconds = 60;

    /// <summary>
    /// Policy name for read operations.
    /// </summary>
    public const string ReadPolicy = "read";

    /// <summary>
    /// Policy name for write operations.
    /// </summary>
    public const string WritePolicy = "write";

    /// <summary>
    /// Policy name for trigger operations.
    /// </summary>
    public const string TriggerPolicy = "trigger";

    /// <summary>
    /// Adds rate limiting services to the service collection.
    /// Configures three different rate limiting policies:
    /// - Read: 100 requests/minute for GET requests
    /// - Write: 30 requests/minute for POST/PUT/PATCH/DELETE
    /// - Trigger: 10 requests/minute for refresh/retry operations
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRateLimiterServices(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Custom rejection handler for JSON error response
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                var rateLimitResponse = CreateRateLimitErrorResponse(context.HttpContext);
                var json = JsonSerializer.Serialize(rateLimitResponse, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await context.HttpContext.Response.WriteAsync(json, cancellationToken);
            };

            // Read policy - 100 requests per minute per IP
            options.AddPolicy(ReadPolicy, context =>
            {
                var ipAddress = GetClientIpAddress(context.Connection.RemoteIpAddress);
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: ipAddress,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = ReadLimit,
                        Window = TimeSpan.FromSeconds(WindowSeconds),
                        SegmentsPerWindow = 6,
                        AutoReplenishment = true
                    });
            });

            // Write policy - 30 requests per minute per IP
            options.AddPolicy(WritePolicy, context =>
            {
                var ipAddress = GetClientIpAddress(context.Connection.RemoteIpAddress);
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: ipAddress,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = WriteLimit,
                        Window = TimeSpan.FromSeconds(WindowSeconds),
                        SegmentsPerWindow = 6,
                        AutoReplenishment = true
                    });
            });

            // Trigger policy - 10 requests per minute per IP (for refresh/retry endpoints)
            options.AddPolicy(TriggerPolicy, context =>
            {
                var ipAddress = GetClientIpAddress(context.Connection.RemoteIpAddress);
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: ipAddress,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = TriggerLimit,
                        Window = TimeSpan.FromSeconds(WindowSeconds),
                        SegmentsPerWindow = 6,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }

    /// <summary>
    /// Gets the client IP address, handling IPv6 localhost mapping.
    /// </summary>
    /// <param name="remoteIpAddress">The remote IP address.</param>
    /// <returns>A string representation of the client IP address.</returns>
    private static string GetClientIpAddress(System.Net.IPAddress? remoteIpAddress)
    {
        if (remoteIpAddress == null)
        {
            return "unknown";
        }

        // Handle IPv6 localhost
        if (System.Net.IPAddress.IsLoopback(remoteIpAddress))
        {
            return "127.0.0.1";
        }

        return remoteIpAddress.ToString();
    }

    /// <summary>
    /// Creates a rate limit error response object.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>An error response object for rate limiting.</returns>
    private static object CreateRateLimitErrorResponse(HttpContext context)
    {
        var requestId = context.TraceIdentifier;
        var resetTime = DateTimeOffset.UtcNow.AddSeconds(WindowSeconds).ToUnixTimeSeconds();

        return new
        {
            error = new
            {
                code = "RATE_LIMITED",
                message = "Rate limit exceeded",
                details = $"Maximum requests per minute exceeded. Retry after {WindowSeconds} seconds.",
                request_id = requestId,
                retry_after = WindowSeconds,
                reset = resetTime
            }
        };
    }
}