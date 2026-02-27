namespace Yallarhorn.Services;

using System.Collections.Concurrent;
using Yallarhorn.Models;

/// <summary>
/// Interface for pipeline metrics collection.
/// Provides thread-safe metrics tracking for downloads and transcodes.
/// </summary>
public interface IPipelineMetrics
{
    /// <summary>
    /// Records that a download has started.
    /// </summary>
    /// <param name="episodeId">The episode ID.</param>
    /// <param name="channelId">The channel ID.</param>
    void RecordDownloadStarted(string episodeId, string channelId);

    /// <summary>
    /// Records that a download has completed successfully.
    /// </summary>
    /// <param name="episodeId">The episode ID.</param>
    /// <param name="duration">The download duration.</param>
    /// <param name="bytesDownloaded">The number of bytes downloaded.</param>
    void RecordDownloadCompleted(string episodeId, TimeSpan duration, long bytesDownloaded);

    /// <summary>
    /// Records that a download has failed.
    /// </summary>
    /// <param name="episodeId">The episode ID.</param>
    /// <param name="errorCategory">The error category.</param>
    void RecordDownloadFailed(string episodeId, ErrorCategory errorCategory);

    /// <summary>
    /// Records that a transcode operation has completed.
    /// </summary>
    /// <param name="episodeId">The episode ID.</param>
    /// <param name="format">The transcode format.</param>
    /// <param name="duration">The transcode duration.</param>
    void RecordTranscodeCompleted(string episodeId, TranscodeFormat format, TimeSpan duration);

    /// <summary>
    /// Updates the queue depth statistics.
    /// Called by DownloadQueueService when queue state changes.
    /// </summary>
    /// <param name="pending">Number of pending items.</param>
    /// <param name="inProgress">Number of in-progress items.</param>
    /// <param name="retrying">Number of items waiting for retry.</param>
    void UpdateQueueDepth(int pending, int inProgress, int retrying);

    /// <summary>
    /// Gets the current pipeline statistics.
    /// </summary>
    /// <returns>A snapshot of the current metrics.</returns>
    PipelineStats GetStats();

    /// <summary>
    /// Resets all metrics to their initial state.
    /// </summary>
    void Reset();
}

/// <summary>
/// Thread-safe metrics collection for the download pipeline.
/// Tracks download counts, durations, bytes, transcodes, and errors.
/// </summary>
public class PipelineMetrics : IPipelineMetrics
{
    // Counters using Interlocked for thread-safety
    private long _downloadsStarted;
    private long _downloadsCompleted;
    private long _downloadsFailed;
    private long _totalBytesDownloaded;

    // Duration tracking for averages
    private long _totalDownloadDurationTicks;
    private readonly object _downloadDurationLock = new();

    // Transcode tracking
    private readonly ConcurrentDictionary<string, long> _transcodeCounts = new();
    private readonly ConcurrentDictionary<string, long> _transcodeDurationTicks = new();
    private readonly ConcurrentDictionary<string, long> _transcodeCountForAverage = new();

    // Error tracking
    private readonly ConcurrentDictionary<string, long> _errorCounts = new();

    // Queue depth (gauge values updated by queue service)
    private int _queuePending;
    private int _queueInProgress;
    private int _queueRetrying;
    private readonly object _queueDepthLock = new();

    /// <summary>
    /// Records that a download has started.
    /// </summary>
    /// <param name="episodeId">The episode ID.</param>
    /// <param name="channelId">The channel ID.</param>
    public void RecordDownloadStarted(string episodeId, string channelId)
    {
        ArgumentException.ThrowIfNullOrEmpty(episodeId);
        ArgumentException.ThrowIfNullOrEmpty(channelId);

        Interlocked.Increment(ref _downloadsStarted);
    }

    /// <summary>
    /// Records that a download has completed successfully.
    /// </summary>
    /// <param name="episodeId">The episode ID.</param>
    /// <param name="duration">The download duration.</param>
    /// <param name="bytesDownloaded">The number of bytes downloaded.</param>
    public void RecordDownloadCompleted(string episodeId, TimeSpan duration, long bytesDownloaded)
    {
        ArgumentException.ThrowIfNullOrEmpty(episodeId);
        
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration cannot be negative.");
        }

        if (bytesDownloaded < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytesDownloaded), "Bytes downloaded cannot be negative.");
        }

        Interlocked.Increment(ref _downloadsCompleted);
        Interlocked.Add(ref _totalBytesDownloaded, bytesDownloaded);

        // Track duration for averaging (need lock for multi-value consistency)
        lock (_downloadDurationLock)
        {
            _totalDownloadDurationTicks += duration.Ticks;
        }
    }

    /// <summary>
    /// Records that a download has failed.
    /// </summary>
    /// <param name="episodeId">The episode ID.</param>
    /// <param name="errorCategory">The error category.</param>
    public void RecordDownloadFailed(string episodeId, ErrorCategory errorCategory)
    {
        ArgumentException.ThrowIfNullOrEmpty(episodeId);

        Interlocked.Increment(ref _downloadsFailed);

        // Track error by category
        var categoryName = errorCategory.ToString();
        _errorCounts.AddOrUpdate(categoryName, 1, (_, count) => count + 1);
    }

    /// <summary>
    /// Records that a transcode operation has completed.
    /// </summary>
    /// <param name="episodeId">The episode ID.</param>
    /// <param name="format">The transcode format.</param>
    /// <param name="duration">The transcode duration.</param>
    public void RecordTranscodeCompleted(string episodeId, TranscodeFormat format, TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrEmpty(episodeId);

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration cannot be negative.");
        }

        var formatName = format.ToString();

        // Increment count
        _transcodeCounts.AddOrUpdate(formatName, 1, (_, count) => count + 1);

        // Track duration for averaging
        _transcodeDurationTicks.AddOrUpdate(formatName, duration.Ticks, (_, ticks) => ticks + duration.Ticks);
        _transcodeCountForAverage.AddOrUpdate(formatName, 1, (_, count) => count + 1);
    }

    /// <summary>
    /// Updates the queue depth statistics.
    /// Called by DownloadQueueService when queue state changes.
    /// </summary>
    /// <param name="pending">Number of pending items.</param>
    /// <param name="inProgress">Number of in-progress items.</param>
    /// <param name="retrying">Number of items waiting for retry.</param>
    public void UpdateQueueDepth(int pending, int inProgress, int retrying)
    {
        if (pending < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pending), "Pending count cannot be negative.");
        }

        if (inProgress < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inProgress), "In-progress count cannot be negative.");
        }

        if (retrying < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retrying), "Retrying count cannot be negative.");
        }

        lock (_queueDepthLock)
        {
            _queuePending = pending;
            _queueInProgress = inProgress;
            _queueRetrying = retrying;
        }
    }

    /// <summary>
    /// Gets the current pipeline statistics.
    /// </summary>
    /// <returns>A snapshot of the current metrics.</returns>
    public PipelineStats GetStats()
    {
        // Read all counters atomically
        var downloadsStarted = Interlocked.Read(ref _downloadsStarted);
        var downloadsCompleted = Interlocked.Read(ref _downloadsCompleted);
        var downloadsFailed = Interlocked.Read(ref _downloadsFailed);
        var totalBytes = Interlocked.Read(ref _totalBytesDownloaded);

        // Calculate average download duration
        TimeSpan? averageDuration = null;
        lock (_downloadDurationLock)
        {
            if (downloadsCompleted > 0)
            {
                var totalTicks = _totalDownloadDurationTicks;
                averageDuration = TimeSpan.FromTicks(totalTicks / downloadsCompleted);
            }
        }

        // Get queue depth
        QueueDepthStats queueDepth;
        lock (_queueDepthLock)
        {
            queueDepth = new QueueDepthStats
            {
                Pending = _queuePending,
                InProgress = _queueInProgress,
                Retrying = _queueRetrying,
            };
        }

        // Build transcode averages
        var transcodeAverages = new Dictionary<string, TimeSpan>();
        foreach (var kvp in _transcodeCounts)
        {
            var formatName = kvp.Key;
            var count = kvp.Value;

            if (count > 0 && _transcodeDurationTicks.TryGetValue(formatName, out var totalTicks))
            {
                transcodeAverages[formatName] = TimeSpan.FromTicks(totalTicks / count);
            }
        }

        return new PipelineStats
        {
            DownloadsStarted = downloadsStarted,
            DownloadsCompleted = downloadsCompleted,
            DownloadsFailed = downloadsFailed,
            AverageDownloadDuration = averageDuration,
            TotalBytesDownloaded = totalBytes,
            TranscodeCounts = new Dictionary<string, long>(_transcodeCounts),
            AverageTranscodeDurations = transcodeAverages,
            ErrorCounts = new Dictionary<string, long>(_errorCounts),
            QueueDepth = queueDepth,
        };
    }

    /// <summary>
    /// Resets all metrics to their initial state.
    /// </summary>
    public void Reset()
    {
        // Reset counters
        Interlocked.Exchange(ref _downloadsStarted, 0);
        Interlocked.Exchange(ref _downloadsCompleted, 0);
        Interlocked.Exchange(ref _downloadsFailed, 0);
        Interlocked.Exchange(ref _totalBytesDownloaded, 0);

        // Reset duration tracking
        lock (_downloadDurationLock)
        {
            _totalDownloadDurationTicks = 0;
        }

        // Clear dictionaries
        _transcodeCounts.Clear();
        _transcodeDurationTicks.Clear();
        _transcodeCountForAverage.Clear();
        _errorCounts.Clear();

        // Reset queue depth
        lock (_queueDepthLock)
        {
            _queuePending = 0;
            _queueInProgress = 0;
            _queueRetrying = 0;
        }
    }
}