namespace Yallarhorn.Extensions;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yallarhorn.Authentication;
using Yallarhorn.Background;
using Yallarhorn.Configuration;
using Yallarhorn.Controllers;
using Yallarhorn.Data;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Data.Seeding;
using Yallarhorn.Services;

/// <summary>
/// Extension methods for service collection configuration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Yallarhorn services to the container.
    /// </summary>
    public static WebApplicationBuilder AddYallarhornServices(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<ServerOptions>(
            builder.Configuration.GetSection(ServerOptions.SectionName));

        // Add controllers
        builder.Services.AddControllers();

        // Register services
        builder.Services.AddHealthChecks();

        // Add OpenAPI
        builder.Services.AddOpenApi();

        return builder;
    }

    /// <summary>
    /// Adds the Yallarhorn services with custom options.
    /// </summary>
    public static IServiceCollection AddYallarhornCoreServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure options
        services.Configure<ServerOptions>(
            configuration.GetSection(ServerOptions.SectionName));

        services.Configure<YallarhornOptions>(
            configuration);

        services.Configure<DatabaseOptions>(
            configuration.GetSection(DatabaseOptions.SectionName));

        services.Configure<TranscodeOptions>(
            configuration.GetSection(TranscodeOptions.SectionName));

        // Register TranscodeOptions as concrete type for services that need it directly
        services.AddSingleton(sp =>
        {
            var options = configuration.GetSection(TranscodeOptions.SectionName).Get<TranscodeOptions>()
                ?? new TranscodeOptions();
            return options;
        });

        // Register download directory for TranscodeService
        var yallarhornOptions = configuration.Get<YallarhornOptions>() ?? new YallarhornOptions();
        services.AddSingleton(sp => yallarhornOptions.DownloadDir);

        // Add memory cache for feed caching
        services.AddMemoryCache();

        // Database
        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
            ?? new DatabaseOptions();
        services.AddDbContext<YallarhornDbContext>(options =>
            options.UseSqlite($"Data Source={databaseOptions.Path}"));

        // Repositories (Scoped)
        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<IEpisodeRepository, EpisodeRepository>();
        services.AddScoped<IDownloadQueueRepository, DownloadQueueRepository>();

        // Database seeding
        services.AddScoped<DevelopmentSeeder>();

        // Phase 3 Services
        services.AddSingleton<IYtDlpClient, YtDlpClient>();
        services.AddSingleton<IFfmpegClient, FfmpegClient>();
        services.AddSingleton<IDownloadCoordinator, DownloadCoordinator>();
        services.AddScoped<ITranscodeService, TranscodeService>();
        services.AddScoped<IChannelRefreshService, ChannelRefreshService>();
        services.AddScoped<IDownloadQueueService, DownloadQueueService>();
        services.AddScoped<IDownloadPipeline, DownloadPipeline>();
        services.AddScoped<IEpisodeCleanupService, EpisodeCleanupService>();
        services.AddSingleton<IPipelineMetrics, PipelineMetrics>();
        services.AddSingleton<IFileService, FileService>();

        // Phase 4 Services
        services.AddSingleton<IRssFeedBuilder, RssFeedBuilder>();
        services.AddSingleton<IAtomFeedBuilder, AtomFeedBuilder>();
        services.AddScoped<IFeedService, FeedService>();
        services.AddScoped<ICombinedFeedService, CombinedFeedService>();
        services.AddSingleton<IFeedCache, FeedCache>();
        services.AddSingleton<IStorageService, StorageService>();

        return services;
    }

    /// <summary>
    /// Validates and binds options from configuration.
    /// </summary>
    public static IServiceCollection AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
    {
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName));

        return services;
    }

    /// <summary>
    /// Adds Yallarhorn authentication services with domain-specific authentication.
    /// Configures separate BasicAuth handlers for feed and admin endpoints.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddYallarhornAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register credential validator as singleton
        services.AddSingleton<ICredentialValidator, CredentialValidator>();

        // Get auth options
        var authSection = configuration.GetSection(AuthOptions.SectionName);
        var authOptions = authSection.Get<AuthOptions>() ?? new AuthOptions();

        // Configure authentication with domain-specific schemes
        var authBuilder = services.AddAuthentication();

        // Configure Feed Authentication scheme if enabled
        if (authOptions.FeedCredentials.Enabled)
        {
            authBuilder.AddScheme<BasicAuthOptions, BasicAuthHandler>(
                AuthorizationPolicies.FeedAuthScheme,
                options =>
                {
                    options.Username = authOptions.FeedCredentials.Username;
                    options.Password = authOptions.FeedCredentials.Password;
                    options.Realm = authOptions.FeedCredentials.Realm;
                });
        }

        // Configure Admin Authentication scheme if enabled
        if (authOptions.AdminAuth.Enabled)
        {
            authBuilder.AddScheme<BasicAuthOptions, BasicAuthHandler>(
                AuthorizationPolicies.AdminAuthScheme,
                options =>
                {
                    options.Username = authOptions.AdminAuth.Username;
                    options.Password = authOptions.AdminAuth.Password;
                    options.Realm = "Yallarhorn Admin";
                });
        }

        // Add authorization with Yallarhorn policies
        services.AddYallarhornAuthorization();

        return services;
    }

    /// <summary>
    /// Adds Yallarhorn authentication and authorization services with conditional policies.
    /// This method configures authentication only if enabled in configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddYallarhornAuthIfNeeded(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Get auth options
        var authSection = configuration.GetSection(AuthOptions.SectionName);
        var authOptions = authSection.Get<AuthOptions>() ?? new AuthOptions();

        // Register credential validator always (for domain-aware validation)
        services.AddSingleton<ICredentialValidator, CredentialValidator>();

        // If no auth is enabled, still add basic authorization services
        // but don't require authentication
        if (!authOptions.FeedCredentials.Enabled && !authOptions.AdminAuth.Enabled)
        {
            // Add default authorization without authentication requirement
            services.AddAuthorization();
            return services;
        }

        // Otherwise, configure full authentication
        return services.AddYallarhornAuthentication(configuration);
    }

    /// <summary>
    /// Adds Yallarhorn background workers as IHostedService.
    /// Workers are registered conditionally based on configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddYallarhornBackgroundWorkers(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Get worker options
        var workerOptions = configuration.GetSection(WorkerOptions.SectionName).Get<WorkerOptions>()
            ?? new WorkerOptions();

        // Register workers only if enabled
        if (workerOptions.Enabled)
        {
            // Register RefreshWorker with configurable interval
            services.AddHostedService(sp => new RefreshWorker(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RefreshWorker>>(),
                workerOptions.RefreshInterval));

            // Register DownloadWorker with configurable interval
            services.AddHostedService(sp => new DownloadWorker(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<IDownloadCoordinator>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DownloadWorker>>(),
                workerOptions.DownloadPollInterval));
        }

        return services;
    }
}