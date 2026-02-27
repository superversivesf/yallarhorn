using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Models.Api;
using Yallarhorn.Services;

namespace Yallarhorn.Controllers;

/// <summary>
/// API controller for system status and health monitoring.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class StatusController : ControllerBase
{
    private readonly IDownloadQueueRepository _queueRepository;
    private readonly IDownloadCoordinator _downloadCoordinator;
    private readonly IPipelineMetrics _pipelineMetrics;
    private readonly IStorageService _storageService;
    private readonly ILogger<StatusController> _logger;
    private static readonly DateTimeOffset StartTime = DateTimeOffset.UtcNow;
    private static readonly string Version = GetAssemblyVersion();

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusController"/> class.
    /// </summary>
    /// <param name="queueRepository">The download queue repository.</param>
    /// <param name="downloadCoordinator">The download coordinator.</param>
    /// <param name="pipelineMetrics">The pipeline metrics service.</param>
    /// <param name="storageService">The storage service.</param>
    /// <param name="logger">The logger.</param>
    public StatusController(
        IDownloadQueueRepository queueRepository,
        IDownloadCoordinator downloadCoordinator,
        IPipelineMetrics pipelineMetrics,
        IStorageService storageService,
        ILogger<StatusController> logger)
    {
        _queueRepository = queueRepository;
        _downloadCoordinator = downloadCoordinator;
        _pipelineMetrics = pipelineMetrics;
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Gets comprehensive system status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>System status information.</returns>
    /// <remarks>
    /// Returns:
    /// - version: Application version from assembly
    /// - uptime_seconds: Application uptime in seconds
    /// - storage: Disk usage information (used, free, total bytes, percentage)
    /// - queue: Download queue counts (pending, in_progress, completed, failed, retrying)
    /// - downloads: Active and total download statistics
    /// - last_refresh: Timestamp of last status refresh
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(SystemStatus), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting system status");

        var uptime = DateTimeOffset.UtcNow - StartTime;

        // Get queue counts
        var pending = await _queueRepository.CountByStatusAsync(QueueStatus.Pending, cancellationToken);
        var inProgress = await _queueRepository.CountByStatusAsync(QueueStatus.InProgress, cancellationToken);
        var completed = await _queueRepository.CountByStatusAsync(QueueStatus.Completed, cancellationToken);
        var failed = await _queueRepository.CountByStatusAsync(QueueStatus.Failed, cancellationToken);
        var retrying = await _queueRepository.CountByStatusAsync(QueueStatus.Retrying, cancellationToken);

        // Get pipeline stats
        var stats = _pipelineMetrics.GetStats();

        // Get storage info
        var storageInfo = await _storageService.GetStorageInfoAsync(cancellationToken);

        var status = new SystemStatus
        {
            Version = Version,
            UptimeSeconds = (long)uptime.TotalSeconds,
            Storage = new StorageInfo
            {
                UsedBytes = storageInfo.UsedBytes,
                FreeBytes = storageInfo.FreeBytes,
                TotalBytes = storageInfo.TotalBytes,
                UsedPercentage = storageInfo.UsedPercentage
            },
            Queue = new QueueInfo
            {
                Pending = pending,
                InProgress = inProgress,
                Completed = completed,
                Failed = failed,
                Retrying = retrying
            },
            Downloads = new DownloadInfo
            {
                Active = _downloadCoordinator.ActiveDownloads,
                CompletedTotal = stats.DownloadsCompleted,
                FailedTotal = stats.DownloadsFailed
            },
            LastRefresh = DateTimeOffset.UtcNow
        };

        return Ok(new ApiResponse<SystemStatus> { Data = status });
    }

    /// <summary>
    /// Gets a lightweight health check status.
    /// </summary>
    /// <returns>Health status.</returns>
    /// <remarks>
    /// Lightweight endpoint for load balancers and monitoring.
    /// Returns status "healthy" or "unhealthy" with timestamp.
    /// </remarks>
    [HttpGet("/api/v1/health")]
    [ProducesResponseType(typeof(HealthStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthStatus), StatusCodes.Status503ServiceUnavailable)]
    public IActionResult GetHealth()
    {
        _logger.LogDebug("Getting health status");

        // Basic health check - can be extended to check database, storage, etc.
        var status = new HealthStatus
        {
            Status = "healthy",
            Timestamp = DateTimeOffset.UtcNow
        };

        return Ok(status);
    }

    /// <summary>
    /// Gets detailed queue status with episode information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Queue status with in-progress and failed downloads.</returns>
    /// <remarks>
    /// Returns:
    /// - pending: Count of pending downloads
    /// - in_progress: List of currently downloading items with episode details
    /// - failed: Recent failed downloads with error messages
    /// </remarks>
    [HttpGet("/api/v1/queue")]
    [ProducesResponseType(typeof(QueueStatus), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueueStatus(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting queue status");

        var pending = await _queueRepository.CountByStatusAsync(QueueStatus.Pending, cancellationToken);

        // Get in-progress queue items with episode details
        var inProgressItems = await _queueRepository.GetByStatusAsync(QueueStatus.InProgress, cancellationToken);
        var inProgressList = new List<InProgressDownload>();

        foreach (var item in inProgressItems)
        {
            if (item.Episode != null)
            {
                inProgressList.Add(new InProgressDownload
                {
                    EpisodeId = item.EpisodeId,
                    VideoId = item.Episode.VideoId,
                    Title = item.Episode.Title,
                    ChannelTitle = item.Episode.Channel?.Title ?? "Unknown",
                    Attempts = item.Attempts,
                    StartedAt = item.Episode.DownloadedAt
                });
            }
        }

        // Get failed queue items
        var failedItems = await _queueRepository.GetByStatusAsync(QueueStatus.Failed, cancellationToken);
        var failedList = new List<FailedDownload>();

        foreach (var item in failedItems)
        {
            if (item.Episode != null)
            {
                failedList.Add(new FailedDownload
                {
                    EpisodeId = item.EpisodeId,
                    VideoId = item.Episode.VideoId,
                    Title = item.Episode.Title,
                    ErrorMessage = item.LastError,
                    Attempts = item.Attempts,
                    MaxAttempts = item.MaxAttempts,
                    FailedAt = item.UpdatedAt
                });
            }
        }

        var response = new QueueStatusResponse
        {
            Pending = pending,
            InProgress = inProgressList,
            Failed = failedList
        };

        return Ok(new ApiResponse<QueueStatusResponse> { Data = response });
    }

    /// <summary>
    /// Gets the assembly version.
    /// </summary>
    private static string GetAssemblyVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version != null
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "1.0.0";
    }
}

/// <summary>
/// Interface for storage operations.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Gets storage information for the download directory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Storage information.</returns>
    Task<StorageInfoResult> GetStorageInfoAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Storage information result.
/// </summary>
public record StorageInfoResult
{
    /// <summary>
    /// Gets or sets the used storage in bytes.
    /// </summary>
    public required long UsedBytes { get; init; }

    /// <summary>
    /// Gets or sets the free storage in bytes.
    /// </summary>
    public required long FreeBytes { get; init; }

    /// <summary>
    /// Gets or sets the total storage in bytes.
    /// </summary>
    public required long TotalBytes { get; init; }

    /// <summary>
    /// Gets or sets the storage usage percentage.
    /// </summary>
    public required double UsedPercentage { get; init; }
}

/// <summary>
/// Default implementation of storage service.
/// </summary>
public class StorageService : IStorageService
{
    private readonly string _downloadPath;
    private readonly ILogger<StorageService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageService"/> class.
    /// </summary>
    /// <param name="downloadPath">The download directory path.</param>
    /// <param name="logger">The logger.</param>
    public StorageService(string downloadPath, ILogger<StorageService> logger)
    {
        _downloadPath = downloadPath ?? throw new ArgumentNullException(nameof(downloadPath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<StorageInfoResult> GetStorageInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure directory exists for accurate drive info
            if (!Directory.Exists(_downloadPath))
            {
                Directory.CreateDirectory(_downloadPath);
            }

            var driveInfo = new DriveInfo(_downloadPath);
            var totalBytes = driveInfo.TotalSize;
            var freeBytes = driveInfo.AvailableFreeSpace;
            var usedBytes = totalBytes - freeBytes;
            var usedPercentage = totalBytes > 0 ? (double)usedBytes / totalBytes * 100 : 0;

            return Task.FromResult(new StorageInfoResult
            {
                UsedBytes = usedBytes,
                FreeBytes = freeBytes,
                TotalBytes = totalBytes,
                UsedPercentage = Math.Round(usedPercentage, 2)
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get storage info for path: {Path}", _downloadPath);

            // Return zeros if we can't get the info
            return Task.FromResult(new StorageInfoResult
            {
                UsedBytes = 0,
                FreeBytes = 0,
                TotalBytes = 0,
                UsedPercentage = 0
            });
        }
    }
}