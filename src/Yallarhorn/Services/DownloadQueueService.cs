namespace Yallarhorn.Services;

using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Models;

/// <summary>
/// Interface for download queue service operations.
/// </summary>
public interface IDownloadQueueService
{
    /// <summary>
    /// Enqueues an episode for download with the specified priority.
    /// </summary>
    /// <param name="episodeId">The episode ID to enqueue.</param>
    /// <param name="priority">Download priority (1-10, lower = higher priority). Defaults to 5.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created queue item.</returns>
    /// <exception cref="InvalidOperationException">Thrown if episode is already queued.</exception>
    Task<DownloadQueue> EnqueueAsync(
        string episodeId,
        int priority = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next pending item ordered by priority ASC, then created_at ASC.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next pending item or null if none available.</returns>
    Task<DownloadQueue?> GetNextPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a queue item as in progress.
    /// </summary>
    /// <param name="queueId">The queue item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if item not found or not in Pending status.</exception>
    Task MarkInProgressAsync(string queueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a queue item as completed.
    /// </summary>
    /// <param name="queueId">The queue item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if item not found or not in InProgress status.</exception>
    Task MarkCompletedAsync(string queueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a queue item as failed with retry scheduling.
    /// Implements exponential backoff: 0, 5min, 30min, 2hr, 8hr.
    /// Max attempts = 5, after which status becomes Failed permanently.
    /// </summary>
    /// <param name="queueId">The queue item ID.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="retryAt">Optional custom retry time. If null, uses exponential backoff.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if item not found or not in InProgress status.</exception>
    Task MarkFailedAsync(
        string queueId,
        string errorMessage,
        DateTimeOffset? retryAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a queue item.
    /// Only Pending and Retrying items can be cancelled.
    /// </summary>
    /// <param name="queueId">The queue item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if item not found or cannot be cancelled.</exception>
    Task CancelAsync(string queueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets items that are ready for retry (NextRetryAt has passed).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Items ready for retry, ordered by priority ASC, NextRetryAt ASC.</returns>
    Task<IEnumerable<DownloadQueue>> GetRetryableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing the download queue lifecycle.
/// </summary>
public class DownloadQueueService : IDownloadQueueService
{
    private readonly IDownloadQueueRepository _repository;

    /// <summary>
    /// Maximum attempts before marking as permanently failed.
    /// </summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// Exponential backoff delays for retries.
    /// Attempt 1: 0 (immediate retry)
    /// Attempt 2: 5 minutes
    /// Attempt 3: 30 minutes
    /// Attempt 4: 2 hours
    /// Attempt 5: 8 hours (if still fails, marked as Failed)
    /// </summary>
    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.Zero,           // Attempt 1: Immediate
        TimeSpan.FromMinutes(5), // Attempt 2
        TimeSpan.FromMinutes(30), // Attempt 3
        TimeSpan.FromHours(2),   // Attempt 4
        TimeSpan.FromHours(8)    // Attempt 5
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadQueueService"/> class.
    /// </summary>
    /// <param name="repository">The download queue repository.</param>
    public DownloadQueueService(IDownloadQueueRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<DownloadQueue> EnqueueAsync(
        string episodeId,
        int priority = 5,
        CancellationToken cancellationToken = default)
    {
        // Check if episode is already queued
        var existing = await _repository.GetByEpisodeIdAsync(episodeId, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException(
                $"Episode {episodeId} is already queued (Queue ID: {existing.Id}, Status: {existing.Status})");
        }

        // Validate priority range
        priority = Math.Clamp(priority, 1, 10);

        var now = DateTimeOffset.UtcNow;
        var queueItem = new DownloadQueue
        {
            Id = Guid.NewGuid().ToString("N"),
            EpisodeId = episodeId,
            Priority = priority,
            Status = QueueStatus.Pending,
            Attempts = 0,
            MaxAttempts = MaxAttempts,
            CreatedAt = now,
            UpdatedAt = now
        };

        return await _repository.AddAsync(queueItem, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DownloadQueue?> GetNextPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _repository.GetPendingAsync(limit: 1, cancellationToken);
        return pending.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task MarkInProgressAsync(string queueId, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(queueId, cancellationToken);

        if (item == null)
        {
            throw new InvalidOperationException($"Queue item {queueId} not found");
        }

        if (item.Status != QueueStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Queue item {queueId} is not in Pending status (current: {item.Status})");
        }

        item.Status = QueueStatus.InProgress;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(item, cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkCompletedAsync(string queueId, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(queueId, cancellationToken);

        if (item == null)
        {
            throw new InvalidOperationException($"Queue item {queueId} not found");
        }

        if (item.Status != QueueStatus.InProgress)
        {
            throw new InvalidOperationException(
                $"Queue item {queueId} is not in InProgress status (current: {item.Status})");
        }

        item.Status = QueueStatus.Completed;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(item, cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkFailedAsync(
        string queueId,
        string errorMessage,
        DateTimeOffset? retryAt = null,
        CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(queueId, cancellationToken);

        if (item == null)
        {
            throw new InvalidOperationException($"Queue item {queueId} not found");
        }

        if (item.Status != QueueStatus.InProgress)
        {
            throw new InvalidOperationException(
                $"Queue item {queueId} is not in InProgress status (current: {item.Status})");
        }

        item.Attempts++;
        item.LastError = errorMessage;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        // Check if max attempts reached
        if (item.Attempts >= MaxAttempts)
        {
            item.Status = QueueStatus.Failed;
            item.NextRetryAt = null;
        }
        else
        {
            item.Status = QueueStatus.Retrying;

            // Calculate retry time
            if (retryAt.HasValue)
            {
                item.NextRetryAt = retryAt.Value;
            }
            else
            {
                // Use exponential backoff based on attempt count
                // After 1st failure: immediate (0 delay)
                // After 2nd failure: 5 min
                // After 3rd failure: 30 min
                // After 4th failure: 2 hours
                // After 5th failure: marked as Failed above
                var delayIndex = Math.Min(item.Attempts - 1, BackoffDelays.Length - 1);
                var delay = BackoffDelays[delayIndex];
                item.NextRetryAt = DateTimeOffset.UtcNow.Add(delay);
            }
        }

        await _repository.UpdateAsync(item, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CancelAsync(string queueId, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(queueId, cancellationToken);

        if (item == null)
        {
            throw new InvalidOperationException($"Queue item {queueId} not found");
        }

        // Only Pending and Retrying can be cancelled
        if (item.Status != QueueStatus.Pending && item.Status != QueueStatus.Retrying)
        {
            throw new InvalidOperationException(
                $"Queue item {queueId} cannot be cancelled (current status: {item.Status})");
        }

        item.Status = QueueStatus.Cancelled;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(item, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DownloadQueue>> GetRetryableAsync(CancellationToken cancellationToken = default)
    {
        return await _repository.GetReadyForRetryAsync(cancellationToken);
    }
}