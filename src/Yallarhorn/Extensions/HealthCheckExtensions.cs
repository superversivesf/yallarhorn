namespace Yallarhorn.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Extension methods for health check configuration.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Adds Yallarhorn health checks.
    /// </summary>
    public static IHealthChecksBuilder AddYallarhornHealthChecks(this IHealthChecksBuilder builder)
    {
        // Add custom health checks here
        // builder.AddCheck<CustomHealthCheck>("custom");
        return builder;
    }

    /// <summary>
    /// Maps custom health check endpoints. Call on WebApplication.
    /// </summary>
    public static WebApplication UseYallarhornHealthChecks(this WebApplication app)
    {
        // Health endpoint with JSON response
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthResponse
        });

        // Liveness probe for Kubernetes
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false // Only check if app is alive, not dependencies
        });

        // Readiness probe for Kubernetes
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        return app;
    }

    private static Task WriteHealthResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                data = e.Value.Data.Any() ? e.Value.Data : null
            })
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        return context.Response.WriteAsync(json);
    }
}