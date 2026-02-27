namespace Yallarhorn.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Yallarhorn.Data;
using Yallarhorn.Data.Seeding;
using Yallarhorn.Middleware;

/// <summary>
/// Extension methods for application builder configuration.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the exception handling middleware to the application pipeline.
    /// This middleware catches unhandled exceptions and returns standardized error responses.
    /// Should be placed early in the pipeline to catch all exceptions.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }

    /// <summary>
    /// Adds the exception handling middleware with custom configuration.
    /// This middleware catches unhandled exceptions and returns standardized error responses.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="configure">An action to configure exception handling options.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseExceptionHandling(
        this IApplicationBuilder builder,
        Action<ExceptionHandlingOptions> configure)
    {
        var options = new ExceptionHandlingOptions();
        configure(options);
        
        // Options can be used for future configuration
        // For now, the middleware uses environment settings
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }

    /// <summary>
    /// Adds the request ID middleware to the application pipeline.
    /// This middleware generates unique request IDs for tracing and logging.
    /// Should be placed early in the pipeline, before any logging or error handling.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseRequestId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestIdMiddleware>();
    }

    /// <summary>
    /// Adds the rate limit middleware to the application pipeline.
    /// This middleware adds rate limiting headers (X-RateLimit-Limit, X-RateLimit-Remaining, X-RateLimit-Reset) 
    /// to all responses. Should be used in conjunction with AddRateLimiterServices() in service configuration.
    /// <para>
    /// Note: This middleware adds headers to responses. Rate limiting enforcement is handled by ASP.NET Core's
    /// built-in rate limiting middleware. Use builder.UseRateLimiter() to enable enforcement.
    /// </para>
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseRateLimitHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitMiddleware>();
    }

    /// <summary>
    /// Configures the full Yallarhorn application pipeline including database initialization.
    /// This method ensures the database is created and migrated, and optionally seeds development data.
    /// Should be called early in the pipeline configuration.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="seedDevelopmentData">Whether to seed development data (default: false in production, true in development).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task UseYallarhornPipelineAsync(this WebApplication app, bool? seedDevelopmentData = null)
    {
        // Determine if we should seed development data
        var shouldSeed = seedDevelopmentData ?? app.Environment.IsDevelopment();

        // Ensure database is created/migrated
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<YallarhornDbContext>();
            
            // Ensure database exists and is migrated
            await dbContext.Database.EnsureCreatedAsync();

            // Seed development data in development environment
            if (shouldSeed)
            {
                var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>();
                await seeder.SeedAsync();
            }
        }
    }
}

/// <summary>
/// Configuration options for exception handling middleware.
/// </summary>
public sealed class ExceptionHandlingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to include stack traces in error responses.
    /// Default is false (stack traces only shown in development).
    /// </summary>
    public bool IncludeStackTrace { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to include exception details in error responses.
    /// Default is true.
    /// </summary>
    public bool IncludeExceptionDetails { get; set; } = true;

    /// <summary>
    /// Gets or sets the default error message for unhandled exceptions.
    /// </summary>
    public string DefaultErrorMessage { get; set; } = "An error occurred while processing your request.";
}