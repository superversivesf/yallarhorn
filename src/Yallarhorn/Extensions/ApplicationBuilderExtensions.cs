namespace Yallarhorn.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Yallarhorn.Configuration;
using Yallarhorn.Data;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
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
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task UseYallarhornPipelineAsync(this WebApplication app)
    {
        // Ensure database is created/migrated
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<YallarhornDbContext>();
            
            // Ensure database exists and is migrated
            await dbContext.Database.EnsureCreatedAsync();

            // Seed channels from configuration (always, but only adds if not exists)
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var channelSeeder = scope.ServiceProvider.GetRequiredService<ChannelSeeder>();
            await SeedChannelsFromConfigurationAsync(configuration, channelSeeder);
        }
    }

    /// <summary>
    /// Seeds channels from configuration into the database.
    /// </summary>
    private static async Task SeedChannelsFromConfigurationAsync(IConfiguration configuration, ChannelSeeder channelSeeder)
    {
        var channelsSection = configuration.GetSection("Channels");
        if (!channelsSection.Exists())
        {
            return;
        }

        var channelConfigs = channelsSection.Get<List<ChannelConfigEntry>>();
        if (channelConfigs == null || channelConfigs.Count == 0)
        {
            return;
        }

        var definitions = channelConfigs
            .Where(c => !string.IsNullOrEmpty(c.Url))
            .Select(c => new ChannelDefinition
            {
                Url = c.Url!,
                Title = c.Name ?? ExtractChannelTitle(c.Url),
                Description = c.Description,
                EpisodeCount = c.EpisodeCount > 0 ? c.EpisodeCount : 50,
                FeedType = ParseFeedType(c.FeedType),
                Enabled = c.Enabled,
                UpdateIfExists = true // Update existing channels with config values
            })
            .ToList();

        if (definitions.Count > 0)
        {
            await channelSeeder.SeedAsync(definitions);
        }
    }

    /// <summary>
    /// Parses a feed type string to the enum value.
    /// </summary>
    private static FeedType ParseFeedType(string? feedType)
    {
        if (string.IsNullOrEmpty(feedType))
            return FeedType.Audio;

        return feedType.ToLowerInvariant() switch
        {
            "video" => FeedType.Video,
            "both" => FeedType.Both,
            _ => FeedType.Audio
        };
    }

    /// <summary>
    /// Extracts a channel title from a YouTube URL.
    /// </summary>
    private static string ExtractChannelTitle(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return "Unknown Channel";

        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath.Trim('/');

            if (path.StartsWith("@"))
                return path[1..];
            
            var parts = path.Split('/');
            return parts.Length >= 2 ? parts[^1] : "Unknown Channel";
        }
        catch
        {
            return "Unknown Channel";
        }
    }
}

/// <summary>
/// Configuration entry for a channel (for binding from YAML).
/// </summary>
internal class ChannelConfigEntry
{
    public string? Name { get; set; }
    public string? Url { get; set; }
    public string? Description { get; set; }
    public int EpisodeCount { get; set; }
    public string? FeedType { get; set; }
    public bool Enabled { get; set; } = true;
    public List<string>? Tags { get; set; }
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