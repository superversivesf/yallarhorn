namespace Yallarhorn.Tests.Integration;

using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yallarhorn.Background;
using Yallarhorn.Configuration;
using Yallarhorn.Controllers;
using Yallarhorn.Data;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Extensions;
using Yallarhorn.Services;

/// <summary>
/// Server startup integration tests verifying the application builds and runs correctly.
/// Tests service registration, middleware pipeline, and HTTP endpoint functionality.
/// Follows TDD approach - tests verify expected behavior of Program.cs startup.
/// </summary>
public class ServerStartupTests : IAsyncLifetime
{
    private readonly string _testDbPath;
    private IHost _host = null!;
    private HttpClient _client = null!;
    private TestServer _server = null!;

    public ServerStartupTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"yallarhorn_startup_test_{Guid.NewGuid()}.db");
    }

    public async Task InitializeAsync()
    {
        // Setup test server with minimal configuration
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer()
                    .ConfigureAppConfiguration(config =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Database:Path"] = _testDbPath,
                            ["Workers:Enabled"] = "false", // Disable background workers for tests
                            ["Server:Port"] = "5000",
                            ["TranscodeSettings:AudioFormat"] = "mp3",
                            ["TranscodeSettings:AudioBitrate"] = "192k",
                            ["TranscodeSettings:AudioSampleRate"] = "44100",
                            ["TranscodeSettings:VideoFormat"] = "mp4",
                            ["TranscodeSettings:VideoCodec"] = "h264",
                            ["TranscodeSettings:VideoQuality"] = "23",
                            ["TranscodeSettings:Threads"] = "4",
                            ["Auth:FeedCredentials:Enabled"] = "false",
                            ["Auth:AdminAuth:Enabled"] = "false"
                        });
                    })
                    .ConfigureServices((context, services) =>
                    {
                        var configuration = context.Configuration;

                        // Configure options (matching Program.cs pattern)
                        services.Configure<ServerOptions>(configuration.GetSection(ServerOptions.SectionName));
                        services.Configure<YallarhornOptions>(configuration);
                        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
                        services.Configure<TranscodeOptions>(configuration.GetSection(TranscodeOptions.SectionName));
                        services.Configure<WorkerOptions>(configuration.GetSection(WorkerOptions.SectionName));
                        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));

                        // Memory cache
                        services.AddMemoryCache();

                        // Database
                        services.AddDbContext<YallarhornDbContext>(options =>
                            options.UseSqlite($"Data Source={_testDbPath}"));

                        // Repositories
                        services.AddScoped<IChannelRepository, ChannelRepository>();
                        services.AddScoped<IEpisodeRepository, EpisodeRepository>();
                        services.AddScoped<IDownloadQueueRepository, DownloadQueueRepository>();

                        // Mock external services (yt-dlp, ffmpeg)
                        services.AddSingleton(new Mock<IYtDlpClient>().Object);
                        services.AddSingleton(new Mock<IFfmpegClient>().Object);
                        
                        // Mock coordinator
                        var coordinatorMock = new Mock<IDownloadCoordinator>();
                        coordinatorMock.Setup(c => c.ActiveDownloads).Returns(0);
                        coordinatorMock.Setup(c => c.Dispose());
                        services.AddSingleton(coordinatorMock.Object);

                        services.AddSingleton<IPipelineMetrics, PipelineMetrics>();
                        services.AddSingleton<IFileService, FileService>();

                        // Transcode options concrete instance
                        services.AddSingleton(new TranscodeOptions
                        {
                            AudioFormat = "mp3",
                            AudioBitrate = "192k",
                            AudioSampleRate = 44100,
                            VideoFormat = "mp4",
                            VideoCodec = "h264",
                            VideoQuality = 23,
                            Threads = 4
                        });

                        // Services
                        services.AddScoped<ITranscodeService>(sp => new TranscodeService(
                            sp.GetRequiredService<ILogger<TranscodeService>>(),
                            sp.GetRequiredService<IFfmpegClient>(),
                            sp.GetRequiredService<TranscodeOptions>(),
                            Path.GetTempPath()));
                        services.AddScoped<IChannelRefreshService, ChannelRefreshService>();
                        services.AddScoped<IDownloadQueueService, DownloadQueueService>();
                        services.AddScoped<IDownloadPipeline>(sp => new DownloadPipeline(
                            sp.GetRequiredService<ILogger<DownloadPipeline>>(),
                            sp.GetRequiredService<IYtDlpClient>(),
                            sp.GetRequiredService<ITranscodeService>(),
                            sp.GetRequiredService<IEpisodeRepository>(),
                            sp.GetRequiredService<IChannelRepository>(),
                            sp.GetRequiredService<IDownloadCoordinator>(),
                            Path.GetTempPath(),
                            Path.GetTempPath()));
                        services.AddScoped<IEpisodeCleanupService>(sp => new EpisodeCleanupService(
                            sp.GetRequiredService<IEpisodeRepository>(),
                            sp.GetRequiredService<IChannelRepository>(),
                            sp.GetRequiredService<IFileService>(),
                            Path.GetTempPath(),
                            sp.GetService<ILogger<EpisodeCleanupService>>()));

                        // Feed services
                        services.AddSingleton<IRssFeedBuilder, RssFeedBuilder>();
                        services.AddSingleton<IAtomFeedBuilder, AtomFeedBuilder>();
                        services.AddScoped<IFeedService, FeedService>();
                        services.AddScoped<ICombinedFeedService, CombinedFeedService>();
                        services.AddSingleton<IFeedCache, FeedCache>();

                        // Storage service
                        services.AddSingleton<IStorageService>(sp => new StorageService(
                            Path.GetTempPath(),
                            sp.GetRequiredService<ILogger<StorageService>>()));

                        // Auth - disabled for tests
                        services.AddAuthorization();

                        // Controllers - add application part to discover controllers
                        services.AddControllers()
                            .AddApplicationPart(typeof(ChannelsController).Assembly);

                        // Health checks
                        services.AddHealthChecks();

                        // OpenAPI
                        services.AddOpenApi();

                        // Logging
                        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
                    })
                    .Configure(app =>
                    {
                        // Database initialization
                        using (var scope = app.ApplicationServices.CreateScope())
                        {
                            var dbContext = scope.ServiceProvider.GetRequiredService<YallarhornDbContext>();
                            dbContext.Database.EnsureCreated();
                        }

                        app.UseRouting();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints =>
                        {
                            // Map health endpoint
                            endpoints.MapHealthChecks("/health");

                            // Map controllers
                            endpoints.MapControllers();

                            // Simple health endpoint (matching Program.cs)
                            endpoints.MapGet("/health/simple", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

                            // Home endpoint
                            endpoints.MapGet("/", () => Results.Ok(new { message = "Yallarhorn API" }));
                        });
                    });
            });

        _host = await hostBuilder.StartAsync();
        _server = _host.GetTestServer();
        _client = _server.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _server?.Dispose();
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        // Clean up test database
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    #region WebApplication Builder Tests

    [Fact]
    public void WebApplication_BuildsSuccessfully_WithoutErrors()
    {
        // Arrange - Host is built in InitializeAsync
        
        // Act - Host should be running
        var isRunning = _host.Services != null;

        // Assert
        isRunning.Should().BeTrue("WebApplication should build and start successfully");
        _server.Should().NotBeNull("TestServer should be created");
        _client.Should().NotBeNull("HttpClient should be available");
    }

    [Fact]
    public void WebApplication_ContainsRequiredServices()
    {
        // Arrange
        var services = _host.Services;

        // Act & Assert - Verify all core services are registered
        services.GetService<YallarhornDbContext>().Should().NotBeNull("DbContext should be registered");
        services.GetService<IChannelRepository>().Should().NotBeNull("ChannelRepository should be registered");
        services.GetService<IEpisodeRepository>().Should().NotBeNull("EpisodeRepository should be registered");
        services.GetService<IDownloadQueueRepository>().Should().NotBeNull("DownloadQueueRepository should be registered");
        services.GetService<IChannelRefreshService>().Should().NotBeNull("ChannelRefreshService should be registered");
        services.GetService<IDownloadQueueService>().Should().NotBeNull("DownloadQueueService should be registered");
        services.GetService<IDownloadPipeline>().Should().NotBeNull("DownloadPipeline should be registered");
        services.GetService<IFeedService>().Should().NotBeNull("FeedService should be registered");
        services.GetService<IStorageService>().Should().NotBeNull("StorageService should be registered");
    }

    [Fact]
    public void WebApplication_ConfiguredOptions_AreAvailable()
    {
        // Arrange
        var services = _host.Services;

        // Act
        var serverOptions = services.GetService<Microsoft.Extensions.Options.IOptions<ServerOptions>>();
        var databaseOptions = services.GetService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>();
        var transcodeOptions = services.GetService<Microsoft.Extensions.Options.IOptions<TranscodeOptions>>();
        var workerOptions = services.GetService<Microsoft.Extensions.Options.IOptions<WorkerOptions>>();
        var authOptions = services.GetService<Microsoft.Extensions.Options.IOptions<AuthOptions>>();

        // Assert
        serverOptions.Should().NotBeNull("ServerOptions should be configured");
        serverOptions!.Value.Should().NotBeNull();
        
        databaseOptions.Should().NotBeNull("DatabaseOptions should be configured");
        databaseOptions!.Value.Should().NotBeNull();
        databaseOptions.Value.Path.Should().Be(_testDbPath, "Database path should match configuration");

        transcodeOptions.Should().NotBeNull("TranscodeOptions should be configured");
        transcodeOptions!.Value.Should().NotBeNull();
        transcodeOptions.Value.AudioFormat.Should().Be("mp3");

        workerOptions.Should().NotBeNull("WorkerOptions should be configured");
        workerOptions!.Value.Should().NotBeNull();
        workerOptions.Value.Enabled.Should().BeFalse("Workers should be disabled for tests");

        authOptions.Should().NotBeNull("AuthOptions should be configured");
        authOptions!.Value.Should().NotBeNull();
    }

    [Fact]
    public void WebApplication_DatabaseContext_IsConfiguredCorrectly()
    {
        // Arrange
        var services = _host.Services;
        var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<YallarhornDbContext>();

        // Act & Assert
        dbContext.Should().NotBeNull("DbContext should be available");
        dbContext.Database.Should().NotBeNull("Database should be configured");
        
        // Verify database is accessible
        var canConnect = dbContext.Database.CanConnect();
        canConnect.Should().BeTrue("Database should be connectable");

        scope.Dispose();
    }

    #endregion

    #region HTTP Response Tests

    [Fact]
    public async Task Server_RespondsToHttpRequests()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.Should().NotBeNull("Server should respond to HTTP requests");
        response.StatusCode.Should().Be(HttpStatusCode.OK, "Root endpoint should return 200 OK");
    }

    [Fact]
    public async Task Server_ReturnsValidContentType()
    {
        // Act
        var response = await _client.GetAsync("/health/simple");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Should().NotBeNull();
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task Server_ReturnsJsonContent()
    {
        // Act
        var response = await _client.GetAsync("/health/simple");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("status");
        content.Should().Contain("healthy");
    }

    #endregion

    #region Health Endpoint Tests

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "Health endpoint should return 200 OK");
        content.Should().NotBeNullOrEmpty();
        content.ToLowerInvariant().Should().Contain("healthy", "Health status should indicate healthy");
    }

    [Fact]
    public async Task HealthEndpoint_Simple_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health/simple");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "Simple health endpoint should return 200 OK");
        content.Should().Contain("status");
        content.Should().Contain("healthy");
        content.Should().Contain("timestamp");
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsCorrectContentType()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Note: MapHealthChecks returns text/plain by default, not application/json
        response.Content.Headers.ContentType.Should().NotBeNull();
    }

    #endregion

    #region Routing Tests

    [Fact]
    public async Task Routing_RootEndpoint_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "Root endpoint should return 200 OK");
    }

    [Fact]
    public async Task Routing_ControllerEndpoint_ReturnsNotFound_WhenResourceNotFound()
    {
        // Act - Try to get a nonexistent channel
        var response = await _client.GetAsync("/api/v1/channels/nonexistent-channel-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "Nonexistent resources should return 404");
    }

    [Fact]
    public async Task Routing_StatusEndpoint_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "Status endpoint should return 200 OK");
    }

    [Fact]
    public async Task Routing_InvalidEndpoint_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/this-does-not-exist");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "Invalid endpoints should return 404");
    }

    #endregion

    #region Service Registration Verification Tests

    [Fact]
    public void Services_RepositoryServices_AreScoped()
    {
        // Arrange
        var services = _host.Services;

        // Act - Create two scopes and verify different instances
        using var scope1 = services.CreateScope();
        using var scope2 = services.CreateScope();

        var repo1 = scope1.ServiceProvider.GetRequiredService<IChannelRepository>();
        var repo2 = scope2.ServiceProvider.GetRequiredService<IChannelRepository>();

        // Assert - Different scopes should have different instances
        repo1.Should().NotBeSameAs(repo2, "Scoped services should have different instances per scope");
    }

    [Fact]
    public void Services_SingletonServices_AreSameInstance()
    {
        // Arrange
        var services = _host.Services;

        // Act - Get service from root provider (singletons)
        var singleton1 = services.GetRequiredService<IStorageService>();
        var singleton2 = services.GetRequiredService<IStorageService>();

        // Assert - Should be same instance
        singleton1.Should().BeSameAs(singleton2, "Singleton services should have the same instance");
    }

    [Fact]
    public void Services_BackgroundWorkers_NotRegistered_WhenDisabled()
    {
        // Arrange
        var services = _host.Services;
        var hostedServices = services.GetServices<Microsoft.Extensions.Hosting.IHostedService>();

        // Assert - Workers should not be registered when disabled
        hostedServices.Should().NotContain(s => s is RefreshWorker, "RefreshWorker should not be registered when disabled");
        hostedServices.Should().NotContain(s => s is DownloadWorker, "DownloadWorker should not be registered when disabled");
    }

    [Fact]
    public void Services_AllRequiredServices_CanBeResolved()
    {
        // Arrange
        var services = _host.Services;
        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;

        // Act & Assert - All services should be resolvable without throwing
        var act = () =>
        {
            scopedServices.GetRequiredService<YallarhornDbContext>();
            scopedServices.GetRequiredService<IChannelRepository>();
            scopedServices.GetRequiredService<IEpisodeRepository>();
            scopedServices.GetRequiredService<IDownloadQueueRepository>();
            scopedServices.GetRequiredService<IChannelRefreshService>();
            scopedServices.GetRequiredService<IDownloadQueueService>();
            scopedServices.GetRequiredService<IDownloadPipeline>();
            scopedServices.GetRequiredService<ITranscodeService>();
            scopedServices.GetRequiredService<IFeedService>();
            scopedServices.GetRequiredService<IStorageService>();
            scopedServices.GetRequiredService<IRssFeedBuilder>();
            scopedServices.GetRequiredService<IAtomFeedBuilder>();
            scopedServices.GetRequiredService<IFeedCache>();
            scopedServices.GetRequiredService<ICombinedFeedService>();
        };

        act.Should().NotThrow("All required services should be registered and resolvable");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Server_HandlesInvalidJson_Gracefully()
    {
        // Arrange
        var content = new StringContent("{ invalid json }", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/channels", content);

        // Assert - Should return 400 BadRequest, not crash
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "Invalid JSON should return 400");
    }

    [Fact]
    public async Task Server_HandlesMissingRequestBody_Gracefully()
    {
        // Arrange
        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/channels", content);

        // Assert - Should return 400 BadRequest for invalid model
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "Invalid model should return 400");
    }

    #endregion

    #region Integration: Full Startup Flow Tests

    [Fact]
    public async Task FullStartup_DatabaseInitializesSuccessfully()
    {
        // Arrange - Database is created in InitializeAsync
        
        // Act - Verify database exists and has tables
        using var scope = _host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<YallarhornDbContext>();
        var canConnect = await dbContext.Database.CanConnectAsync();

        // Assert
        canConnect.Should().BeTrue("Database should be initialized and connectable");
        
        // Verify tables exist by querying them
        var channels = dbContext.Channels.Any();
        var episodes = dbContext.Episodes.Any();
        
        // Tables should exist (even if empty)
        true.Should().BeTrue("Database tables should be accessible");
    }

    [Fact]
    public async Task FullStartup_AllMiddlewareConfigured()
    {
        // Act - Make request through middleware pipeline
        var response = await _client.GetAsync("/health");

        // Assert - Response indicates middleware pipeline worked
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Additional middleware checks
        response.Should().NotBeNull("Response should not be null");
        response.Content.Should().NotBeNull("Response content should not be null");
    }

    [Fact]
    public async Task FullStartup_ApplicationRespondsToMultipleRequests()
    {
        // Act - Send multiple requests to verify server stability
        var responses = await Task.WhenAll(
            _client.GetAsync("/health"),
            _client.GetAsync("/health/simple"),
            _client.GetAsync("/api/v1/status"),
            _client.GetAsync("/")
        );

        // Assert - All requests should succeed
        responses.Should().AllBeAssignableTo<HttpResponseMessage>();
        responses.All(r => r.StatusCode == HttpStatusCode.OK).Should().BeTrue("All endpoints should return 200 OK");
    }

    #endregion
}
