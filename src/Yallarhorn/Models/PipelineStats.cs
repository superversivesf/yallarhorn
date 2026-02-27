namespace Yallarhorn.Models;

/// <summary>
/// Represents error categories for download failures.
/// </summary>
public enum ErrorCategory
{
    /// <summary>
    /// Network-related errors (timeouts, connection failures).
    /// </summary>
    NetworkError,

    /// <summary>
    /// Video not found or has been removed.
    /// </summary>
    VideoNotFound,

    /// <summary>
    /// Video is private or age-restricted.
    /// </summary>
    VideoPrivate,

    /// <summary>
    /// Transcoding failed.
    /// </summary>
    TranscodeError,

    /// <summary>
    /// Download was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Unknown or unexpected error.
    /// </summary>
    Unknown,
}

/// <summary>
/// Represents transcode format types.
/// </summary>
public enum TranscodeFormat
{
    /// <summary>
    /// Audio format (MP3/M4A).
    /// </summary>
    Audio,

    /// <summary>
    /// Video format (MP4).
    /// </summary>
    Video,
}

/// <summary>
/// Represents queue depth information.
/// </summary>
public record QueueDepthStats
{
    /// <summary>
    /// Gets the number of pending downloads.
    /// </summary>
    public required int Pending { get; init; }

    /// <summary>
    /// Gets the number of in-progress downloads.
    /// </summary>
    public required int InProgress { get; init; }

    /// <summary>
    /// Gets the number of downloads waiting for retry.
    /// </summary>
    public required int Retrying { get; init; }

    /// <summary>
    /// Gets the total queue depth.
    /// </summary>
    public int Total => Pending + InProgress + Retrying;
}

/// <summary>
/// Represents statistics for the download pipeline.
/// </summary>
public record PipelineStats
{
    /// <summary>
    /// Gets the total number of downloads started.
    /// </summary>
    public required long DownloadsStarted { get; init; }

    /// <summary>
    /// Gets the total number of downloads completed successfully.
    /// </summary>
    public required long DownloadsCompleted { get; init; }

    /// <summary>
    /// Gets the total number of downloads that failed.
    /// </summary>
    public required long DownloadsFailed { get; init; }

    /// <summary>
    /// Gets the average download duration, or null if no downloads completed.
    /// </summary>
    public TimeSpan? AverageDownloadDuration { get; init; }

    /// <summary>
    /// Gets the total bytes downloaded.
    /// </summary>
    public required long TotalBytesDownloaded { get; init; }

    /// <summary>
    /// Gets the transcode counts by format (Audio, Video).
    /// </summary>
    public required IReadOnlyDictionary<string, long> TranscodeCounts { get; init; }

    /// <summary>
    /// Gets the average transcode duration by format.
    /// </summary>
    public required IReadOnlyDictionary<string, TimeSpan> AverageTranscodeDurations { get; init; }

    /// <summary>
    /// Gets the error counts by category.
    /// </summary>
    public required IReadOnlyDictionary<string, long> ErrorCounts { get; init; }

    /// <summary>
    /// Gets the current queue depth statistics.
    /// </summary>
    public required QueueDepthStats QueueDepth { get; init; }

    /// <summary>
    /// Gets the download success rate (0.0 to 1.0), or null if no downloads.
    /// </summary>
    public double? SuccessRate
    {
        get
        {
            var total = DownloadsCompleted + DownloadsFailed;
            return total > 0 ? (double)DownloadsCompleted / total : null;
        }
    }

    /// <summary>
    /// Gets the timestamp when these stats were collected.
    /// </summary>
    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.UtcNow;
}