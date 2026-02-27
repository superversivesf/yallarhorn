namespace Yallarhorn.Tests.Unit.Controllers;

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yallarhorn.Controllers;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Models;
using Yallarhorn.Models.Api;
using Yallarhorn.Services;

public class StatusControllerTests : IDisposable
{
    private readonly Mock<IDownloadQueueRepository> _queueRepositoryMock;
    private readonly Mock<IDownloadCoordinator> _downloadCoordinatorMock;
    private readonly Mock<IPipelineMetrics> _pipelineMetricsMock;
    private readonly Mock<IStorageService> _storageServiceMock;
    private readonly Mock<ILogger<StatusController>> _loggerMock;
    private readonly StatusController _controller;

    public StatusControllerTests()
    {
        _queueRepositoryMock = new Mock<IDownloadQueueRepository>();
        _downloadCoordinatorMock = new Mock<IDownloadCoordinator>();
        _pipelineMetricsMock = new Mock<IPipelineMetrics>();
        _storageServiceMock = new Mock<IStorageService>();
        _loggerMock = new Mock<ILogger<StatusController>>();

        _controller = new StatusController(
            _queueRepositoryMock.Object,
            _downloadCoordinatorMock.Object,
            _pipelineMetricsMock.Object,
            _storageServiceMock.Object,
            _loggerMock.Object);

        // Set up HttpContext with Request
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost:8080");
        httpContext.Request.PathBase = new PathString("");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public void Dispose()
    {
        // Nothing to dispose
    }

    #region GetStatus Tests

    [Fact]
    public async Task GetStatus_ShouldReturnSystemStatus_WithCorrectVersion()
    {
        // Arrange
        SetupQueueRepositoryCounts();
        SetupPipelineMetrics();
        SetupStorageService();

        // Act
        var result = await _controller.GetStatus();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<SystemStatus>>().Subject;

        response.Data.Version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetStatus_ShouldReturnUptimeInSeconds()
    {
        // Arrange
        SetupQueueRepositoryCounts();
        SetupPipelineMetrics();
        SetupStorageService();

        // Act
        var result = await _controller.GetStatus();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<SystemStatus>>().Subject;

        // Uptime should be >= 0
        response.Data.UptimeSeconds.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task GetStatus_ShouldReturnStorageInfo()
    {
        // Arrange
        SetupQueueRepositoryCounts();
        SetupPipelineMetrics();
        _storageServiceMock
            .Setup(s => s.GetStorageInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageInfoResult
            {
                UsedBytes = 50L * 1024 * 1024 * 1024, // 50 GB
                FreeBytes = 50L * 1024 * 1024 * 1024, // 50 GB
                TotalBytes = 100L * 1024 * 1024 * 1024, // 100 GB
                UsedPercentage = 50.0
            });

        // Act
        var result = await _controller.GetStatus();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<SystemStatus>>().Subject;

        response.Data.Storage.Should().NotBeNull();
        response.Data.Storage.UsedBytes.Should().Be(50L * 1024 * 1024 * 1024);
        response.Data.Storage.FreeBytes.Should().Be(50L * 1024 * 1024 * 1024);
        response.Data.Storage.TotalBytes.Should().Be(100L * 1024 * 1024 * 1024);
        response.Data.Storage.UsedPercentage.Should().Be(50.0);
    }

    [Fact]
    public async Task GetStatus_ShouldReturnQueueCounts()
    {
        // Arrange
        _queueRepositoryMock
            .Setup(r => r.CountByStatusAsync(QueueStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);
        _queueRepositoryMock
            .Setup(r => r.CountByStatusAsync(QueueStatus.InProgress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _queueRepositoryMock
            .Setup(r => r.CountByStatusAsync(QueueStatus.Completed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(245);
        _queueRepositoryMock
            .Setup(r => r.CountByStatusAsync(QueueStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        _queueRepositoryMock
            .Setup(r => r.CountByStatusAsync(QueueStatus.Retrying, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        SetupPipelineMetrics();
        SetupStorageService();

        // Act
        var result = await _controller.GetStatus();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<SystemStatus>>().Subject;

        response.Data.Queue.Should().NotBeNull();
        response.Data.Queue.Pending.Should().Be(10);
        response.Data.Queue.InProgress.Should().Be(2);
        response.Data.Queue.Completed.Should().Be(245);
        response.Data.Queue.Failed.Should().Be(5);
        response.Data.Queue.Retrying.Should().Be(3);
    }

    [Fact]
    public async Task GetStatus_ShouldReturnDownloadStats()
    {
        // Arrange
        SetupQueueRepositoryCounts();
        _downloadCoordinatorMock
            .Setup(c => c.ActiveDownloads)
            .Returns(3);
        _pipelineMetricsMock
            .Setup(m => m.GetStats())
            .Returns(new PipelineStats
            {
                DownloadsStarted = 100,
                DownloadsCompleted = 85,
                DownloadsFailed = 5,
                TotalBytesDownloaded = 1024L * 1024 * 1024 * 10,
                TranscodeCounts = new Dictionary<string, long>(),
                AverageTranscodeDurations = new Dictionary<string, TimeSpan>(),
                ErrorCounts = new Dictionary<string, long>(),
                QueueDepth = new QueueDepthStats { Pending = 10, InProgress = 3, Retrying = 2 }
            });
        SetupStorageService();

        // Act
        var result = await _controller.GetStatus();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<SystemStatus>>().Subject;

        response.Data.Downloads.Should().NotBeNull();
        response.Data.Downloads.Active.Should().Be(3);
        response.Data.Downloads.CompletedTotal.Should().Be(85);
        response.Data.Downloads.FailedTotal.Should().Be(5);
    }

    [Fact]
    public async Task GetStatus_ShouldReturnLastRefreshTimestamp()
    {
        // Arrange
        SetupQueueRepositoryCounts();
        SetupPipelineMetrics();
        SetupStorageService();

        // Act
        var result = await _controller.GetStatus();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<SystemStatus>>().Subject;

        response.Data.LastRefresh.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetStatus_ShouldCallAllDependencies()
    {
        // Arrange
        SetupQueueRepositoryCounts();
        SetupPipelineMetrics();
        SetupStorageService();

        // Act
        await _controller.GetStatus();

        // Assert
        _queueRepositoryMock.Verify(r => r.CountByStatusAsync(QueueStatus.Pending, It.IsAny<CancellationToken>()), Times.Once);
        _queueRepositoryMock.Verify(r => r.CountByStatusAsync(QueueStatus.InProgress, It.IsAny<CancellationToken>()), Times.Once);
        _queueRepositoryMock.Verify(r => r.CountByStatusAsync(QueueStatus.Completed, It.IsAny<CancellationToken>()), Times.Once);
        _queueRepositoryMock.Verify(r => r.CountByStatusAsync(QueueStatus.Failed, It.IsAny<CancellationToken>()), Times.Once);
        _queueRepositoryMock.Verify(r => r.CountByStatusAsync(QueueStatus.Retrying, It.IsAny<CancellationToken>()), Times.Once);
        _downloadCoordinatorMock.Verify(c => c.ActiveDownloads, Times.Once);
        _pipelineMetricsMock.Verify(m => m.GetStats(), Times.Once);
        _storageServiceMock.Verify(s => s.GetStorageInfoAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetHealth Tests

    [Fact]
    public void GetHealth_ShouldReturnHealthyStatus()
    {
        // Act
        var result = _controller.GetHealth();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var status = okResult.Value.Should().BeOfType<HealthStatus>().Subject;

        status.Status.Should().Be("healthy");
    }

    [Fact]
    public void GetHealth_ShouldReturnTimestamp()
    {
        // Act
        var result = _controller.GetHealth();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var status = okResult.Value.Should().BeOfType<HealthStatus>().Subject;

        status.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GetHealth_ShouldBeRoutedAtApiV1Health()
    {
        // Arrange & Act
        var attribute = typeof(StatusController)
            .GetMethod("GetHealth")?
            .GetCustomAttributes(typeof(HttpGetAttribute), false)
            .FirstOrDefault() as HttpGetAttribute;

        // Assert
        attribute.Should().NotBeNull();
        attribute!.Template.Should().Be("/api/v1/health");
    }

    #endregion

    #region GetQueueStatus Tests

    [Fact]
    public async Task GetQueueStatus_ShouldReturnPendingCount()
    {
        // Arrange
        _queueRepositoryMock
            .Setup(r => r.CountByStatusAsync(QueueStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(15);
        _queueRepositoryMock
            .Setup(r => r.GetByStatusAsync(QueueStatus.InProgress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadQueue>());
        _queueRepositoryMock
            .Setup(r => r.GetByStatusAsync(QueueStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadQueue>());

        // Act
        var result = await _controller.GetQueueStatus();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<QueueStatusResponse>>().Subject;

        response.Data.Pending.Should().Be(15);
    }

    [Fact]
    public async Task GetQueueStatus_ShouldReturnInProgressItems_WithEpisodeDetails()
    {
        // Arrange
        var channel = new Channel
        {
            Id = "ch-1",
            Title = "Test Channel",
            Url = "https://youtube.com/@test"
        };

        var episode = new Episode
        {
            Id = "ep-1",
            VideoId = "video1",
            Title = "Test Episode",
            ChannelId = "ch-1",
            Channel = channel,
            DownloadedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        var queueItem = new DownloadQueue
        {
            Id = "dq-1",
            EpisodeId = "ep-1",
            Episode = episode,
            Status = QueueStatus.InProgress,
            Attempts = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _queueRepositoryMock
            .Setup(r => r.CountByStatusAsync(QueueStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        _queueRepositoryMock
            .Setup(r => r.GetByStatusAsync(QueueStatus.InProgress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadQueue> { queueItem });
        _queueRepositoryMock
            .Setup(r => r.GetByStatusAsync(QueueStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadQueue>());

        // Act
        var result = await _controller.GetQueueStatus();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<QueueStatusResponse>>().Subject;

        response.Data.InProgress.Should().HaveCount(1);
        response.Data.InProgress[0].EpisodeId.Should().Be("ep-1");
        response.Data.InProgress[0].VideoId.Should().Be("video1");
        response.Data.InProgress[0].Title.Should().Be("Test Episode");
        response.Data.InProgress[0].ChannelTitle.Should().Be("Test Channel");
        response.Data.InProgress[0].Attempts.Should().Be(1);
    }

    [Fact]
    public async Task GetQueueStatus_ShouldReturnFailedItems_WithErrorMessages()
    {
        // Arrange
        var episode = new Episode
        {
            Id = "ep-2",
            VideoId = "video2",
            Title = "Failed Episode",
            ChannelId = "ch-1",
            Channel = new Channel { Id = "ch-1", Title = "Test Channel", Url = "https://youtube.com/@test" }
        };

        var queueItem = new DownloadQueue
        {
            Id = "dq-2",
            EpisodeId = "ep-2",
            Episode = episode,
            Status = QueueStatus.Failed,
            Attempts = 3,
            MaxAttempts = 3,
            LastError = "Network timeout after 30 seconds",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        };

        _queueRepositoryMock
            .Setup(r => r.CountByStatusAsync(QueueStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _queueRepositoryMock
            .Setup(r => r.GetByStatusAsync(QueueStatus.InProgress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadQueue>());
        _queueRepositoryMock
            .Setup(r => r.GetByStatusAsync(QueueStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadQueue> { queueItem });

        // Act
        var result = await _controller.GetQueueStatus();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<QueueStatusResponse>>().Subject;

        response.Data.Failed.Should().HaveCount(1);
        response.Data.Failed[0].EpisodeId.Should().Be("ep-2");
        response.Data.Failed[0].VideoId.Should().Be("video2");
        response.Data.Failed[0].Title.Should().Be("Failed Episode");
        response.Data.Failed[0].ErrorMessage.Should().Be("Network timeout after 30 seconds");
        response.Data.Failed[0].Attempts.Should().Be(3);
        response.Data.Failed[0].MaxAttempts.Should().Be(3);
    }

    [Fact]
    public async Task GetQueueStatus_ShouldReturnEmptyLists_WhenNoItems()
    {
        // Arrange
        _queueRepositoryMock
            .Setup(r => r.CountByStatusAsync(QueueStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _queueRepositoryMock
            .Setup(r => r.GetByStatusAsync(QueueStatus.InProgress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadQueue>());
        _queueRepositoryMock
            .Setup(r => r.GetByStatusAsync(QueueStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadQueue>());

        // Act
        var result = await _controller.GetQueueStatus();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<QueueStatusResponse>>().Subject;

        response.Data.Pending.Should().Be(0);
        response.Data.InProgress.Should().BeEmpty();
        response.Data.Failed.Should().BeEmpty();
    }

    [Fact]
    public async Task GetQueueStatus_ShouldBeRoutedAtApiV1Queue()
    {
        // Arrange & Act
        var attribute = typeof(StatusController)
            .GetMethod("GetQueueStatus")?
            .GetCustomAttributes(typeof(HttpGetAttribute), false)
            .FirstOrDefault() as HttpGetAttribute;

        // Assert
        attribute.Should().NotBeNull();
        attribute!.Template.Should().Be("/api/v1/queue");
    }

    [Fact]
    public async Task GetQueueStatus_ShouldSkipItemsWithoutEpisodes()
    {
        // Arrange
        var queueItemWithoutEpisode = new DownloadQueue
        {
            Id = "dq-3",
            EpisodeId = "ep-missing",
            Episode = null, // No episode
            Status = QueueStatus.InProgress,
            Attempts = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _queueRepositoryMock
            .Setup(r => r.CountByStatusAsync(QueueStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _queueRepositoryMock
            .Setup(r => r.GetByStatusAsync(QueueStatus.InProgress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadQueue> { queueItemWithoutEpisode });
        _queueRepositoryMock
            .Setup(r => r.GetByStatusAsync(QueueStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadQueue>());

        // Act
        var result = await _controller.GetQueueStatus();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<QueueStatusResponse>>().Subject;

        // Should skip items without episodes
        response.Data.InProgress.Should().BeEmpty();
    }

    [Fact]
    public async Task GetQueueStatus_HandlesChannelNull_WhenEpisodeChannelIsNull()
    {
        // Arrange
        var episode = new Episode
        {
            Id = "ep-3",
            VideoId = "video3",
            Title = "Episode Without Channel",
            ChannelId = "ch-missing",
            Channel = null // No channel
        };

        var queueItem = new DownloadQueue
        {
            Id = "dq-4",
            EpisodeId = "ep-3",
            Episode = episode,
            Status = QueueStatus.InProgress,
            Attempts = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _queueRepositoryMock
            .Setup(r => r.CountByStatusAsync(QueueStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _queueRepositoryMock
            .Setup(r => r.GetByStatusAsync(QueueStatus.InProgress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadQueue> { queueItem });
        _queueRepositoryMock
            .Setup(r => r.GetByStatusAsync(QueueStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadQueue>());

        // Act
        var result = await _controller.GetQueueStatus();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<QueueStatusResponse>>().Subject;

        response.Data.InProgress.Should().HaveCount(1);
        response.Data.InProgress[0].ChannelTitle.Should().Be("Unknown");
    }

    #endregion

    #region Helper Methods

    private void SetupQueueRepositoryCounts()
    {
        _queueRepositoryMock
            .Setup(r => r.CountByStatusAsync(It.IsAny<QueueStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private void SetupPipelineMetrics()
    {
        _pipelineMetricsMock
            .Setup(m => m.GetStats())
            .Returns(new PipelineStats
            {
                DownloadsStarted = 0,
                DownloadsCompleted = 0,
                DownloadsFailed = 0,
                TotalBytesDownloaded = 0,
                TranscodeCounts = new Dictionary<string, long>(),
                AverageTranscodeDurations = new Dictionary<string, TimeSpan>(),
                ErrorCounts = new Dictionary<string, long>(),
                QueueDepth = new QueueDepthStats { Pending = 0, InProgress = 0, Retrying = 0 }
            });

        _downloadCoordinatorMock
            .Setup(c => c.ActiveDownloads)
            .Returns(0);
    }

    private void SetupStorageService()
    {
        _storageServiceMock
            .Setup(s => s.GetStorageInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageInfoResult
            {
                UsedBytes = 0,
                FreeBytes = 0,
                TotalBytes = 0,
                UsedPercentage = 0
            });
    }

    #endregion
}