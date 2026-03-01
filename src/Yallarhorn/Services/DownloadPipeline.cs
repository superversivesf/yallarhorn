namespace Yallarhorn.Services;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yallarhorn.Configuration;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Models;

/// <summary>
/// Interface for the download pipeline orchestrator.
/// Coordinates the complete download-to-feed pipeline for episodes.
/// </summary>
public interface IDownloadPipeline
{
    /// <summary>
    /// Executes the complete download pipeline for an episode.
    /// </summary>
    /// <param name="episodeId">The episode ID to process.</param>
    /// <param name="progressCallback">Optional callback for pipeline progress updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the pipeline execution.</returns>
    Task<PipelineResult> ExecuteAsync(
        string episodeId,
        Action<PipelineProgress>? progressCallback = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of a pipeline execution.
/// </summary>
public record PipelineResult
{
    /// <summary>
    /// Gets a value indicating whether the pipeline completed successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the episode ID that was processed.
    /// </summary>
    public required string EpisodeId { get; init; }

    /// <summary>
    /// Gets the path to the downloaded file.
    /// </summary>
    public string? DownloadPath { get; init; }

    /// <summary>
    /// Gets the path to the transcoded audio file.
    /// </summary>
    public string? AudioPath { get; init; }

    /// <summary>
    /// Gets the path to the transcoded video file.
    /// </summary>
    public string? VideoPath { get; init; }

    /// <summary>
    /// Gets the error message if the pipeline failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the total duration of the pipeline execution.
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Represents progress information for the download pipeline.
/// </summary>
public record PipelineProgress
{
    /// <summary>
    /// Gets the current pipeline stage.
    /// </summary>
    public required PipelineStage Stage { get; init; }

    /// <summary>
    /// Gets the episode ID being processed.
    /// </summary>
    public required string EpisodeId { get; init; }

    /// <summary>
    /// Gets the current progress within the stage (0-100).
    /// </summary>
    public double? Progress { get; init; }

    /// <summary>
    /// Gets the status message for the current stage.
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
/// Represents the stages of the download pipeline.
/// </summary>
public enum PipelineStage
{
    /// <summary>
    /// Pipeline is starting.
    /// </summary>
    Starting,

    /// <summary>
    /// Downloading video via yt-dlp.
    /// </summary>
    Downloading,

    /// <summary>
    /// Transcoding media files.
    /// </summary>
    Transcoding,

    /// <summary>
    /// Cleaning up temporary files.
    /// </summary>
    Cleanup,

    /// <summary>
    /// Pipeline completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Pipeline failed.
    /// </summary>
    Failed
}

/// <summary>
/// Orchestrates the complete download-to-feed pipeline for podcast episodes.
/// Handles downloading, transcoding, status updates, and cleanup.
/// </summary>
public class DownloadPipeline : IDownloadPipeline
{
    private readonly ILogger<DownloadPipeline> _logger;
    private readonly IYtDlpClient _ytDlpClient;
    private readonly ITranscodeService _transcodeService;
    private readonly IEpisodeRepository _episodeRepository;
    private readonly IChannelRepository _channelRepository;
    private readonly IDownloadCoordinator _downloadCoordinator;
    private readonly string _downloadDirectory;
    private readonly string _tempDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadPipeline"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="ytDlpClient">yt-dlp client for downloading.</param>
    /// <param name="transcodeService">Transcode service for media conversion.</param>
    /// <param name="episodeRepository">Episode repository.</param>
    /// <param name="channelRepository">Channel repository.</param>
    /// <param name="downloadCoordinator">Download coordinator for concurrency control.</param>
    /// <param name="yallarhornOptions">Yallarhorn configuration options.</param>
    public DownloadPipeline(
        ILogger<DownloadPipeline> logger,
        IYtDlpClient ytDlpClient,
        ITranscodeService transcodeService,
        IEpisodeRepository episodeRepository,
        IChannelRepository channelRepository,
        IDownloadCoordinator downloadCoordinator,
        IOptions<YallarhornOptions> yallarhornOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ytDlpClient = ytDlpClient ?? throw new ArgumentNullException(nameof(ytDlpClient));
        _transcodeService = transcodeService ?? throw new ArgumentNullException(nameof(transcodeService));
        _episodeRepository = episodeRepository ?? throw new ArgumentNullException(nameof(episodeRepository));
        _channelRepository = channelRepository ?? throw new ArgumentNullException(nameof(channelRepository));
        _downloadCoordinator = downloadCoordinator ?? throw new ArgumentNullException(nameof(downloadCoordinator));
        var opts = yallarhornOptions?.Value ?? throw new ArgumentNullException(nameof(yallarhornOptions));
        _downloadDirectory = opts.DownloadDir ?? throw new InvalidOperationException("DownloadDir not configured");
        _tempDirectory = opts.TempDir ?? throw new InvalidOperationException("TempDir not configured");
    }

    /// <inheritdoc/>
    public async Task<PipelineResult> ExecuteAsync(
        string episodeId,
        Action<PipelineProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(episodeId);

        var stopwatch = Stopwatch.StartNew();
        Episode? episode = null;

        _logger.LogInformation("Starting download pipeline for episode {EpisodeId}", episodeId);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Execute within coordinator's concurrency control
            return await _downloadCoordinator.ExecuteDownloadAsync(
                async ct =>
                {
                    episode = await LoadEpisodeAsync(episodeId, ct);
                    return await ExecutePipelineAsync(episode, progressCallback, stopwatch, ct);
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Pipeline cancelled for episode {EpisodeId}", episodeId);

            // If we have an episode, try to mark it as failed
            if (episode != null)
            {
                try
                {
                    await MarkEpisodeFailedAsync(episode, "Pipeline cancelled", CancellationToken.None);
                }
                catch
                {
                    // Ignore errors when marking as failed - cancellation takes priority
                }
            }

            throw; // Re-throw to let caller know it was cancelled
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline failed for episode {EpisodeId}", episodeId);

            stopwatch.Stop();

            return new PipelineResult
            {
                Success = false,
                EpisodeId = episodeId,
                Error = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    private async Task<PipelineResult> ExecutePipelineAsync(
        Episode? episode,
        Action<PipelineProgress>? progressCallback,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Report starting
        ReportProgress(progressCallback, PipelineStage.Starting, episode?.Id ?? "unknown", null, "Starting pipeline");

        // Step 1: Validate episode
        if (episode == null)
        {
            return CreateFailedResult("unknown", "Episode not found", stopwatch);
        }

        // Step 2: Load channel
        var channel = await LoadChannelAsync(episode.ChannelId, cancellationToken);
        if (channel == null)
        {
            return await CreateFailedResultWithUpdateAsync(
                episode,
                "Channel not found",
                stopwatch,
                cancellationToken);
        }

        // Step 3: Download video
        cancellationToken.ThrowIfCancellationRequested();

        await UpdateEpisodeStatusAsync(episode, EpisodeStatus.Downloading, cancellationToken);

        ReportProgress(progressCallback, PipelineStage.Downloading, episode.Id, null, "Downloading video");

        string downloadedFilePath;
        try
        {
            downloadedFilePath = await DownloadVideoAsync(
                episode,
                channel,
                progress => ReportDownloadProgress(progressCallback, episode.Id, progress),
                cancellationToken);
        }
        catch (YtDlpException ex)
        {
            _logger.LogError(ex, "Download failed for episode {EpisodeId}", episode.Id);

            return await CreateFailedResultWithUpdateAsync(
                episode,
                $"Download failed: {ex.Message}",
                stopwatch,
                cancellationToken,
                downloadedFilePath: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Download failed for episode {EpisodeId}", episode.Id);

            return await CreateFailedResultWithUpdateAsync(
                episode,
                $"Download failed: {ex.Message}",
                stopwatch,
                cancellationToken,
                downloadedFilePath: null);
        }

        // Step 4: Transcode
        cancellationToken.ThrowIfCancellationRequested();

        await UpdateEpisodeStatusAsync(episode, EpisodeStatus.Processing, cancellationToken);

        ReportProgress(progressCallback, PipelineStage.Transcoding, episode.Id, null, "Transcoding media");

        TranscodeServiceResult transcodeResult;
        try
        {
            transcodeResult = await _transcodeService.TranscodeAsync(
                downloadedFilePath,
                channel,
                episode,
                progress => ReportTranscodeProgress(progressCallback, episode.Id, progress),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Transcoding failed for episode {EpisodeId}", episode.Id);

            // Cleanup temp file and return failure
            await CleanupTempFileAsync(downloadedFilePath);

            return await CreateFailedResultWithUpdateAsync(
                episode,
                $"Transcoding failed: {ex.Message}",
                stopwatch,
                cancellationToken,
                downloadedFilePath);
        }

        if (!transcodeResult.Success)
        {
            _logger.LogError("Transcoding failed for episode {EpisodeId}: {Error}",
                episode.Id, transcodeResult.ErrorMessage);

            // Cleanup temp file
            await CleanupTempFileAsync(downloadedFilePath);

            return await CreateFailedResultWithUpdateAsync(
                episode,
                $"Transcoding failed: {transcodeResult.ErrorMessage}",
                stopwatch,
                cancellationToken,
                downloadedFilePath);
        }

        // Step 5: Update episode with file info
        episode.DownloadedAt = DateTimeOffset.UtcNow;
        await _episodeRepository.UpdateAsync(episode, cancellationToken);

        // Step 6: Cleanup temp files
        cancellationToken.ThrowIfCancellationRequested();

        ReportProgress(progressCallback, PipelineStage.Cleanup, episode.Id, null, "Cleaning up temporary files");

        await CleanupTempFileAsync(downloadedFilePath);

        // Step 7: Mark as completed
        await UpdateEpisodeStatusAsync(episode, EpisodeStatus.Completed, cancellationToken);

        stopwatch.Stop();

        ReportProgress(progressCallback, PipelineStage.Completed, episode.Id, 100, "Pipeline completed");

        _logger.LogInformation(
            "Pipeline completed for episode {EpisodeId} in {Duration}s",
            episode.Id, stopwatch.Elapsed.TotalSeconds);

        return new PipelineResult
        {
            Success = true,
            EpisodeId = episode.Id,
            DownloadPath = downloadedFilePath,
            AudioPath = transcodeResult.AudioPath,
            VideoPath = transcodeResult.VideoPath,
            Duration = stopwatch.Elapsed
        };
    }

    private async Task<Episode?> LoadEpisodeAsync(string episodeId, CancellationToken cancellationToken)
    {
        var episode = await _episodeRepository.GetByIdAsync(episodeId, cancellationToken);

        if (episode == null)
        {
            _logger.LogWarning("Episode {EpisodeId} not found", episodeId);
        }

        return episode;
    }

    private async Task<Channel?> LoadChannelAsync(string channelId, CancellationToken cancellationToken)
    {
        var channel = await _channelRepository.GetByIdAsync(channelId, cancellationToken);

        if (channel == null)
        {
            _logger.LogWarning("Channel {ChannelId} not found", channelId);
        }

        return channel;
    }

    private async Task<string> DownloadVideoAsync(
        Episode episode,
        Channel channel,
        Action<DownloadProgress>? progressCallback,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Downloading video for episode {EpisodeId}", episode.Id);

        // Generate temp download path
        var tempPath = GenerateTempDownloadPath(episode.VideoId);

        // Ensure temp directory exists
        var directory = Path.GetDirectoryName(tempPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Build video URL from video ID
        var videoUrl = $"https://www.youtube.com/watch?v={episode.VideoId}";

        _logger.LogDebug("Downloading from {Url} to {Path}", videoUrl, tempPath);

        var downloadedPath = await _ytDlpClient.DownloadVideoAsync(
            videoUrl,
            tempPath,
            progressCallback,
            cancellationToken);

        _logger.LogInformation("Download completed for episode {EpisodeId}: {Path}", episode.Id, downloadedPath);

        return downloadedPath;
    }

    private string GenerateTempDownloadPath(string videoId)
    {
        return Path.Combine(_tempDirectory, "downloads", $"{videoId}.mp4");
    }

    private async Task UpdateEpisodeStatusAsync(
        Episode episode,
        EpisodeStatus status,
        CancellationToken cancellationToken)
    {
        episode.Status = status;
        episode.UpdatedAt = DateTimeOffset.UtcNow;

        await _episodeRepository.UpdateAsync(episode, cancellationToken);

        _logger.LogDebug("Updated episode {EpisodeId} status to {Status}", episode.Id, status);
    }

    private async Task CleanupTempFileAsync(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogDebug("Cleaned up temp file: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup temp file: {FilePath}", filePath);
        }
    }

    private async Task MarkEpisodeFailedAsync(
        Episode episode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        episode.Status = EpisodeStatus.Failed;
        episode.ErrorMessage = errorMessage;
        episode.UpdatedAt = DateTimeOffset.UtcNow;

        await _episodeRepository.UpdateAsync(episode, cancellationToken);

        _logger.LogWarning("Episode {EpisodeId} marked as failed: {Error}", episode.Id, errorMessage);
    }

    private void ReportProgress(
        Action<PipelineProgress>? progressCallback,
        PipelineStage stage,
        string episodeId,
        double? progress,
        string? message)
    {
        if (progressCallback == null)
            return;

        try
        {
            progressCallback(new PipelineProgress
            {
                Stage = stage,
                EpisodeId = episodeId,
                Progress = progress,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in progress callback for episode {EpisodeId}", episodeId);
        }
    }

    private void ReportDownloadProgress(
        Action<PipelineProgress>? progressCallback,
        string episodeId,
        DownloadProgress progress)
    {
        ReportProgress(
            progressCallback,
            PipelineStage.Downloading,
            episodeId,
            progress.Progress,
            $"Downloading: {progress.Progress:F1}%");
    }

    private void ReportTranscodeProgress(
        Action<PipelineProgress>? progressCallback,
        string episodeId,
        TranscodeProgress progress)
    {
        ReportProgress(
            progressCallback,
            PipelineStage.Transcoding,
            episodeId,
            progress.Progress,
            progress.Time.HasValue
                ? $"Transcoding: {progress.Time.Value:hh\\:mm\\:ss}"
                : "Transcoding...");
    }

    private PipelineResult CreateFailedResult(
        string episodeId,
        string error,
        Stopwatch stopwatch)
    {
        stopwatch.Stop();

        return new PipelineResult
        {
            Success = false,
            EpisodeId = episodeId,
            Error = error,
            Duration = stopwatch.Elapsed
        };
    }

    private async Task<PipelineResult> CreateFailedResultWithUpdateAsync(
        Episode episode,
        string error,
        Stopwatch stopwatch,
        CancellationToken cancellationToken,
        string? downloadedFilePath = null)
    {
        // Mark episode as failed
        await MarkEpisodeFailedAsync(episode, error, cancellationToken);

        // Cleanup any downloaded temp file
        if (downloadedFilePath != null)
        {
            await CleanupTempFileAsync(downloadedFilePath);
        }

        stopwatch.Stop();

        return new PipelineResult
        {
            Success = false,
            EpisodeId = episode.Id,
            Error = error,
            Duration = stopwatch.Elapsed
        };
    }
}