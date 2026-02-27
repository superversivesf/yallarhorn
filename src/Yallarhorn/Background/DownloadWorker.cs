namespace Yallarhorn.Background;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yallarhorn.Data.Entities;
using Yallarhorn.Services;

/// <summary>
/// Background worker that continuously processes the download queue.
/// Polls for pending items, processes them through the download pipeline,
/// respects concurrency limits, handles retries, and supports graceful shutdown.
/// </summary>
public class DownloadWorker : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDownloadCoordinator _coordinator;
    private readonly ILogger<DownloadWorker> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly CancellationTokenSource _cts;
    private Task? _processingLoop;
    private readonly object _lock = new();
    private bool _isStarted;
    private bool _isStopped;

    /// <summary>
    /// Gets the default poll interval (5 seconds).
    /// </summary>
    public static TimeSpan DefaultPollInterval => TimeSpan.FromSeconds(5);

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadWorker"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="coordinator">The download coordinator for concurrency control.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="pollInterval">The interval between polls when queue is idle. Default is 5 seconds.</param>
    public DownloadWorker(
        IServiceScopeFactory scopeFactory,
        IDownloadCoordinator coordinator,
        ILogger<DownloadWorker> logger,
        TimeSpan? pollInterval = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pollInterval = pollInterval ?? DefaultPollInterval;
        _cts = new CancellationTokenSource();

        _logger.LogInformation(
            "DownloadWorker created with poll interval of {Interval}",
            _pollInterval);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_isStarted)
            {
                _logger.LogWarning("DownloadWorker already started, ignoring duplicate StartAsync call");
                return Task.CompletedTask;
            }

            _isStarted = true;
        }

        _logger.LogInformation("DownloadWorker starting");

        // Start the processing loop
        _processingLoop = ProcessLoopAsync();

        _logger.LogInformation("DownloadWorker started - processing loop running");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_isStopped)
            {
                _logger.LogWarning("DownloadWorker already stopped, ignoring duplicate StopAsync call");
                return;
            }

            _isStopped = true;
        }

        _logger.LogInformation("DownloadWorker stopping - signaling cancellation");

        // Signal cancellation to stop the processing loop
        _cts.Cancel();

        // Wait for the processing loop to complete
        if (_processingLoop != null)
        {
            _logger.LogInformation("Waiting for in-flight downloads to complete");

            try
            {
                await _processingLoop;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("DownloadWorker processing loop cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DownloadWorker processing loop completed with error during shutdown");
            }
        }

        _logger.LogInformation("DownloadWorker stopped");
    }

    /// <summary>
    /// Main processing loop that continuously polls and processes queue items.
    /// </summary>
    private async Task ProcessLoopAsync()
    {
        _logger.LogDebug("Processing loop started");

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                // Create a scope for each iteration to resolve scoped services
                using var scope = _scopeFactory.CreateScope();
                var queueService = scope.ServiceProvider.GetRequiredService<IDownloadQueueService>();
                var pipeline = scope.ServiceProvider.GetRequiredService<IDownloadPipeline>();

                // First, check for retryable items
                var retryableItems = await queueService.GetRetryableAsync(_cts.Token);
                var retryList = retryableItems.ToList();

                if (retryList.Count > 0)
                {
                    _logger.LogInformation(
                        "Found {Count} retryable item(s) ready for processing",
                        retryList.Count);

                    // Process retryable items sequentially to maintain order and avoid overwhelming
                    foreach (var retryItem in retryList)
                    {
                        if (_cts.Token.IsCancellationRequested)
                        {
                            break;
                        }

                        await ProcessQueueItemAsync(retryItem, queueService, pipeline);
                    }
                }

                // Then, process pending items
                var pendingItem = await queueService.GetNextPendingAsync(_cts.Token);

                if (pendingItem != null)
                {
                    _logger.LogDebug(
                        "Processing pending queue item {QueueId} for episode {EpisodeId}",
                        pendingItem.Id,
                        pendingItem.EpisodeId);

                    await ProcessQueueItemAsync(pendingItem, queueService, pipeline);
                }
                else
                {
                    // No pending items - wait before polling again
                    _logger.LogDebug("No pending items in queue, waiting {Interval} before next poll", _pollInterval);

                    try
                    {
                        await Task.Delay(_pollInterval, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during shutdown
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                // Log error but continue processing
                _logger.LogError(ex, "Error in processing loop, continuing...");

                // Brief delay before retrying to avoid tight error loop
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogDebug("Processing loop ended");
    }

    /// <summary>
    /// Processes a single queue item through the download pipeline.
    /// </summary>
    /// <param name="queueItem">The queue item to process.</param>
    /// <param name="queueService">The queue service.</param>
    /// <param name="pipeline">The download pipeline.</param>
    private async Task ProcessQueueItemAsync(DownloadQueue queueItem, IDownloadQueueService queueService, IDownloadPipeline pipeline)
    {
        if (_cts.Token.IsCancellationRequested)
        {
            _logger.LogDebug(
                "Skipping queue item {QueueId} - shutdown in progress",
                queueItem.Id);
            return;
        }

        _logger.LogInformation(
            "Processing queue item {QueueId} for episode {EpisodeId} (status: {Status}, attempt: {Attempt}/{MaxAttempts})",
            queueItem.Id,
            queueItem.EpisodeId,
            queueItem.Status,
            queueItem.Attempts + 1,
            queueItem.MaxAttempts);

        // Mark as in-progress
        try
        {
            await queueService.MarkInProgressAsync(queueItem.Id, _cts.Token);
        }
        catch (InvalidOperationException ex)
        {
            // Item was already processed or modified concurrently
            _logger.LogWarning(
                ex,
                "Failed to mark queue item {QueueId} as in-progress, skipping",
                queueItem.Id);
            return;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Cancelled marking queue item {QueueId} as in-progress",
                queueItem.Id);
            return;
        }

        try
        {
            // Execute the download pipeline
            var result = await pipeline.ExecuteAsync(
                queueItem.EpisodeId,
                progressCallback: null,
                _cts.Token);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Pipeline completed successfully for episode {EpisodeId} in {Duration}s",
                    result.EpisodeId,
                    result.Duration.TotalSeconds);

                await queueService.MarkCompletedAsync(queueItem.Id, _cts.Token);
            }
            else
            {
                _logger.LogWarning(
                    "Pipeline failed for episode {EpisodeId}: {Error}",
                    result.EpisodeId,
                    result.Error);

                await queueService.MarkFailedAsync(
                    queueItem.Id,
                    result.Error ?? "Unknown error",
                    retryAt: null,
                    _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Pipeline cancelled for episode {EpisodeId}",
                queueItem.EpisodeId);

            // Don't mark as failed on cancellation - let it be retried later
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Pipeline threw exception for episode {EpisodeId}",
                queueItem.EpisodeId);

            try
            {
                await queueService.MarkFailedAsync(
                    queueItem.Id,
                    ex.Message,
                    retryAt: null,
                    CancellationToken.None); // Use None to ensure we record the failure
            }
            catch (Exception markFailedEx)
            {
                _logger.LogError(
                    markFailedEx,
                    "Failed to mark queue item {QueueId} as failed",
                    queueItem.Id);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}