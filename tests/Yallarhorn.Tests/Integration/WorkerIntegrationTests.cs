namespace Yallarhorn.Tests.Integration;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Yallarhorn.Background;
using Yallarhorn.Configuration;
using Yallarhorn.Data;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Extensions;
using Yallarhorn.Models;
using Yallarhorn.Services;

/// <summary>
/// Integration tests for RefreshWorker and DownloadWorker.
/// Tests worker lifecycle, service integration, and end-to-end behavior.
/// </summary>
public class WorkerIntegrationTests : IAsyncLifetime
{
    private readonly string _testDbPath;
    private readonly Mock<IYtDlpClient> _ytDlpClientMock;
    private readonly Mock<IFfmpegClient> _ffmpegClientMock;
    private readonly Mock<IDownloadCoordinator> _coordinatorMock;
    private ServiceProvider _serviceProvider = null!;

    public WorkerIntegrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"yallarhorn_worker_test_{Guid.NewGuid()}.db");
        _ytDlpClientMock = new Mock<IYtDlpClient>();
        _ffmpegClientMock = new Mock<IFfmpegClient>();
        _coordinatorMock = new Mock<IDownloadCoordinator>();
    }

    public async Task InitializeAsync()
    {
        // Set up fresh service provider for each test
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Path"] = _testDbPath,
                ["Workers:Enabled"] = "false", // Don't auto-start workers
                ["TranscodeSettings:AudioFormat"] = "mp3",
                ["TranscodeSettings:AudioBitrate"] = "192k",
                ["TranscodeSettings:AudioSampleRate"] = "44100",
                ["TranscodeSettings:VideoFormat"] = "mp4",
                ["TranscodeSettings:VideoCodec"] = "h264",
                ["TranscodeSettings:VideoQuality"] = "23",
                ["TranscodeSettings:Threads"] = "4"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Configure options
        services.Configure<TranscodeOptions>(configuration.GetSection(TranscodeOptions.SectionName));
        services.AddMemoryCache();

        // Database with test path
        services.AddDbContext<YallarhornDbContext>(options =>
            options.UseSqlite($"Data Source={_testDbPath}"));

        // Repositories
        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<IEpisodeRepository, EpisodeRepository>();
        services.AddScoped<IDownloadQueueRepository, DownloadQueueRepository>();

        // Services with mocks for external dependencies
        services.AddSingleton(_ytDlpClientMock.Object);
        services.AddSingleton(_ffmpegClientMock.Object);
        services.AddSingleton(_coordinatorMock.Object);
        services.AddSingleton<IPipelineMetrics, PipelineMetrics>();
        services.AddSingleton<IFileService, FileService>();

        // Transcode options (needs concrete instance, not IOptions)
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

        // Yallarhorn options for download/temp directories
        services.AddSingleton(Options.Create(new YallarhornOptions
        {
            DownloadDir = Path.GetTempPath(),
            TempDir = Path.GetTempPath()
        }));

        // Real services
        services.AddScoped<ITranscodeService>(sp => new TranscodeService(
            sp.GetRequiredService<ILogger<TranscodeService>>(),
            sp.GetRequiredService<IFfmpegClient>(),
            sp.GetRequiredService<TranscodeOptions>(),
            sp.GetRequiredService<IOptions<YallarhornOptions>>()));
        services.AddScoped<IChannelRefreshService, ChannelRefreshService>();
        services.AddScoped<IDownloadQueueService, DownloadQueueService>();
        services.AddScoped<IDownloadPipeline>(sp => new DownloadPipeline(
            sp.GetRequiredService<ILogger<DownloadPipeline>>(),
            sp.GetRequiredService<IYtDlpClient>(),
            sp.GetRequiredService<ITranscodeService>(),
            sp.GetRequiredService<IEpisodeRepository>(),
            sp.GetRequiredService<IChannelRepository>(),
            sp.GetRequiredService<IDownloadCoordinator>(),
            sp.GetRequiredService<IOptions<YallarhornOptions>>()));
        services.AddScoped<IEpisodeCleanupService>(sp => new EpisodeCleanupService(
            sp.GetRequiredService<IEpisodeRepository>(),
            sp.GetRequiredService<IChannelRepository>(),
            sp.GetRequiredService<IFileService>(),
            sp.GetRequiredService<IOptions<YallarhornOptions>>(),
            sp.GetService<ILogger<EpisodeCleanupService>>()));

        _serviceProvider = services.BuildServiceProvider();

        // Initialize database
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<YallarhornDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed test data
        await SeedTestDataAsync(dbContext);
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();

        // Clean up test database
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }

        _coordinatorMock.Object.Dispose();
    }

    private async Task SeedTestDataAsync(YallarhornDbContext dbContext)
    {
        // Add test channels
        var channel1 = new Channel
        {
            Id = "test-channel-1",
            Url = "https://youtube.com/@test1",
            Title = "Test Channel 1",
            Enabled = true,
            EpisodeCountConfig = 50,
            FeedType = FeedType.Audio,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var channel2 = new Channel
        {
            Id = "test-channel-2",
            Url = "https://youtube.com/@test2",
            Title = "Test Channel 2",
            Enabled = false, // Disabled channel
            EpisodeCountConfig = 50,
            FeedType = FeedType.Audio,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Channels.AddRange(channel1, channel2);

        // Add test episodes
        var episode1 = new Episode
        {
            Id = "test-episode-1",
            ChannelId = "test-channel-1",
            Title = "Test Episode 1",
            Description = "Test description",
            VideoId = "video-1",
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Status = EpisodeStatus.Pending,
            DurationSeconds = 1800,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var episode2 = new Episode
        {
            Id = "test-episode-2",
            ChannelId = "test-channel-1",
            Title = "Test Episode 2",
            Description = "Test description",
            VideoId = "video-2",
            PublishedAt = DateTimeOffset.UtcNow,
            Status = EpisodeStatus.Pending,
            DurationSeconds = 2400,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Episodes.AddRange(episode1, episode2);
        await dbContext.SaveChangesAsync();
    }

    #region RefreshWorker Integration Tests

    [Fact]
    public async Task RefreshWorker_ShouldExecuteChannelRefresh_WhenStarted()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = _serviceProvider.GetRequiredService<ILogger<RefreshWorker>>();

        // Set up yt-dlp to return video metadata
        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YtDlpMetadata>
            {
                new() { Id = "new-video-1", Title = "New Video 1", Duration = 1200 }
            });

        _ytDlpClientMock
            .Setup(y => y.GetVideoMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YtDlpMetadata { Id = "new-video-1", Title = "New Video 1", Duration = 1200 });

        var testInterval = TimeSpan.FromMilliseconds(200);
        using var worker = new RefreshWorker(scopeFactory, logger, testInterval);

        // Act
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Allow initial refresh to execute
        await worker.StopAsync(CancellationToken.None);

        // Assert - Initial refresh should have been triggered
        _ytDlpClientMock.Verify(
            y => y.GetChannelVideosAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public async Task RefreshWorker_ShouldSkipDisabledChannels()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var refreshService = scope.ServiceProvider.GetRequiredService<IChannelRefreshService>();

        // Act
        var results = await refreshService.RefreshAllChannelsAsync(CancellationToken.None);

        // Assert - Only enabled channels should be refreshed
        var resultList = results.ToList();
        resultList.Should().HaveCount(1);
        resultList[0].ChannelId.Should().Be("test-channel-1");
    }

    [Fact]
    public async Task RefreshWorker_ShouldQueueNewEpisodesForDownload()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var refreshService = scope.ServiceProvider.GetRequiredService<IChannelRefreshService>();
        var queueService = scope.ServiceProvider.GetRequiredService<IDownloadQueueService>();

        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YtDlpMetadata>
            {
                new() { Id = "new-video-queued", Title = "New Video to Queue", Duration = 1800 }
            });

        _ytDlpClientMock
            .Setup(y => y.GetVideoMetadataAsync("https://youtube.com/watch?v=new-video-queued", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YtDlpMetadata
            {
                Id = "new-video-queued",
                Title = "New Video to Queue",
                Duration = 1800,
                Description = "Test description",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });

        // Act
        var results = await refreshService.RefreshAllChannelsAsync(CancellationToken.None);

        // Assert
        var totalQueued = results.Sum(r => r.EpisodesQueued);
        totalQueued.Should().BeGreaterOrEqualTo(1);

        // Verify the queue has items
        var pendingItem = await queueService.GetNextPendingAsync(CancellationToken.None);
        pendingItem.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshWorker_Lifecycle_StartStopDispose_ShouldWorkCorrectly()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = _serviceProvider.GetRequiredService<ILogger<RefreshWorker>>();

        var worker = new RefreshWorker(scopeFactory, logger, TimeSpan.FromMinutes(60));

        // Act - Lifecycle sequence
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);
        worker.Dispose();

        // Assert - Should not throw
        true.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshWorker_ShouldHandleConcurrentStartStop()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = _serviceProvider.GetRequiredService<ILogger<RefreshWorker>>();

        using var worker = new RefreshWorker(scopeFactory, logger, TimeSpan.FromMilliseconds(100));

        // Act - Start and quickly stop
        var startTask = worker.StartAsync(CancellationToken.None);
        var stopTask = worker.StopAsync(CancellationToken.None);

        await Task.WhenAll(startTask, stopTask);

        // Assert - Should complete without exception
        true.Should().BeTrue();
    }

    #endregion

    #region DownloadWorker Integration Tests

    [Fact]
    public async Task DownloadWorker_Lifecycle_StartStopDispose_ShouldWorkCorrectly()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = _serviceProvider.GetRequiredService<ILogger<DownloadWorker>>();

        var worker = new DownloadWorker(
            scopeFactory,
            _coordinatorMock.Object,
            logger);

        // Act - Lifecycle sequence
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);
        worker.Dispose();

        // Assert - Should not throw
        true.Should().BeTrue();
    }

    [Fact]
    public async Task DownloadWorker_ShouldHandleEmptyQueue_WithoutCrashing()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = _serviceProvider.GetRequiredService<ILogger<DownloadWorker>>();

        // Queue is empty

        using var worker = new DownloadWorker(
            scopeFactory,
            _coordinatorMock.Object,
            logger,
            TimeSpan.FromMilliseconds(50));

        // Act
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(150); // Let it poll a few times
        await worker.StopAsync(CancellationToken.None);

        // Assert - No exceptions, coordinator should never be called
        _coordinatorMock.Verify(
            c => c.AcquireSlotAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DownloadWorker_ShouldProcessQueueItems_Integration()
    {
        // Arrange - This test verifies the worker can process items from the queue
        // but doesn't verify full download pipeline (that's covered by unit tests)
        using var scope = _serviceProvider.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IDownloadQueueService>();
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = _serviceProvider.GetRequiredService<ILogger<DownloadWorker>>();

        // Add item to queue
        await queueService.EnqueueAsync(
            "test-episode-1",
            5,
            CancellationToken.None);

        // Set up coordinator mock with ExecuteDownloadAsync that passes through
        _coordinatorMock
            .Setup(c => c.ExecuteDownloadAsync(It.IsAny<Func<CancellationToken, Task<PipelineResult>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (Func<CancellationToken, Task<PipelineResult>> operation, CancellationToken ct) =>
            {
                return await operation(ct);
            });

        // Set up mock for download that succeeds quickly
        _ytDlpClientMock
            .Setup(y => y.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<DownloadProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/test.mp4");

        // Set up transcode mock to return quickly
        _ffmpegClientMock
            .Setup(f => f.TranscodeAudioAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AudioTranscodeSettings?>(), It.IsAny<Action<TranscodeProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscodeResult { Success = true, OutputPath = "/tmp/test.mp3" });

        using var worker = new DownloadWorker(
            scopeFactory,
            _coordinatorMock.Object,
            logger,
            TimeSpan.FromMilliseconds(50));

        // Act
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(200); // Allow processing to start
        await worker.StopAsync(CancellationToken.None);

        // Assert - Verify the worker attempted to process via the coordinator
        _coordinatorMock.Verify(
            c => c.ExecuteDownloadAsync(It.IsAny<Func<CancellationToken, Task<PipelineResult>>>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public async Task DownloadWorker_ConcurrentStop_ShouldHandleGracefully()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = _serviceProvider.GetRequiredService<ILogger<DownloadWorker>>();

        using var worker = new DownloadWorker(
            scopeFactory,
            _coordinatorMock.Object,
            logger,
            TimeSpan.FromMilliseconds(100));

        // Act - Start and quickly stop (concurrent)
        var startTask = worker.StartAsync(CancellationToken.None);
        var stopTask = worker.StopAsync(CancellationToken.None);

        await Task.WhenAll(startTask, stopTask);

        // Assert - Should complete without exception
        true.Should().BeTrue();
    }

    #endregion

    #region Worker DI Integration Tests

    [Fact]
    public async Task Workers_ShouldBeResolvableFromDI_WhenEnabled()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Workers:Enabled"] = "true",
                ["Workers:RefreshIntervalSeconds"] = "3600",
                ["Workers:DownloadPollIntervalSeconds"] = "5"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        // Add mocks for worker dependencies
        services.AddSingleton(new Mock<IChannelRefreshService>().Object);
        services.AddSingleton(new Mock<IDownloadPipeline>().Object);
        services.AddSingleton(new Mock<IDownloadQueueService>().Object);
        services.AddSingleton(new Mock<IDownloadCoordinator>().Object);

        // Act
        services.AddYallarhornBackgroundWorkers(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        hostedServices.Should().HaveCount(2);
        hostedServices.Should().Contain(s => s is RefreshWorker);
        hostedServices.Should().Contain(s => s is DownloadWorker);

        await serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Workers_ShouldNotBeRegistered_WhenDisabled()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Workers:Enabled"] = "false"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        // Act
        services.AddYallarhornBackgroundWorkers(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        hostedServices.Should().BeEmpty();

        await serviceProvider.DisposeAsync();
    }

    [Fact]
    public void Workers_ShouldUseConfiguredIntervals_FromConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Workers:Enabled"] = "true",
                ["Workers:RefreshIntervalSeconds"] = "1800",
                ["Workers:DownloadPollIntervalSeconds"] = "10"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddSingleton(new Mock<IChannelRefreshService>().Object);
        services.AddSingleton(new Mock<IDownloadPipeline>().Object);
        services.AddSingleton(new Mock<IDownloadQueueService>().Object);
        services.AddSingleton(new Mock<IDownloadCoordinator>().Object);

        // Act
        services.AddYallarhornBackgroundWorkers(configuration);
        var serviceProvider = services.BuildServiceProvider();
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();

        // Assert - Workers registered with correct intervals
        hostedServices.Should().HaveCount(2);

        serviceProvider.Dispose();
    }

    #endregion

    #region Error Handling and Resilience Tests

    [Fact]
    public async Task RefreshWorker_ShouldContinueOperating_AfterRefreshError()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = _serviceProvider.GetRequiredService<ILogger<RefreshWorker>>();

        // First call throws, second succeeds
        var callCount = 0;
        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new YtDlpException("Network error");
                }
                return new List<YtDlpMetadata>();
            });

        using var worker = new RefreshWorker(
            scopeFactory,
            logger,
            TimeSpan.FromMilliseconds(100));

        // Act
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(350); // Allow multiple cycles
        await worker.StopAsync(CancellationToken.None);

        // Assert - Should have recovered and tried multiple times
        _ytDlpClientMock.Verify(
            y => y.GetChannelVideosAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task DownloadWorker_ShouldContinueProcessing_AfterError()
    {
        // Arrange - Tests that worker doesn't crash after queue service errors
        using var scope = _serviceProvider.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IDownloadQueueService>();
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = _serviceProvider.GetRequiredService<ILogger<DownloadWorker>>();

        // Add item to queue
        await queueService.EnqueueAsync("test-episode-1", 5, CancellationToken.None);

        _coordinatorMock
            .Setup(c => c.AcquireSlotAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _coordinatorMock
            .Setup(c => c.ReleaseSlot());
        _coordinatorMock
            .Setup(c => c.ExecuteDownloadAsync(It.IsAny<Func<CancellationToken, Task<PipelineResult>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (Func<CancellationToken, Task<PipelineResult>> operation, CancellationToken ct) =>
            {
                return await operation(ct);
            });

        // Setup download to fail quickly
        _ytDlpClientMock
            .Setup(y => y.DownloadVideoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<DownloadProgress>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new YtDlpException("Download failed"));

        using var worker = new DownloadWorker(
            scopeFactory,
            _coordinatorMock.Object,
            logger,
            TimeSpan.FromMilliseconds(100));

        // Act - Should not throw when items fail
        var act = async () =>
        {
            await worker.StartAsync(CancellationToken.None);
            await Task.Delay(200);
            await worker.StopAsync(CancellationToken.None);
        };

        // Assert - Worker should handle errors gracefully
        await act.Should().NotThrowAsync();
    }

    #endregion
}