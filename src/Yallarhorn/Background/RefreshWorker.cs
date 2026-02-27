namespace Yallarhorn.Background;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yallarhorn.Services;

/// <summary>
/// Background worker that periodically refreshes all enabled channels.
/// Runs as an IHostedService with configurable poll intervals.
/// </summary>
public class RefreshWorker : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RefreshWorker> _logger;
    private readonly TimeSpan _pollInterval;
    private Timer? _timer;
    private readonly CancellationTokenSource _cts;
    private Task? _currentRefreshTask;
    private readonly object _lock = new();
    private bool _isStarted;
    private bool _isStopped;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshWorker"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="pollInterval">The interval between refresh cycles. Default is 1 hour.</param>
    public RefreshWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<RefreshWorker> logger,
        TimeSpan? pollInterval = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pollInterval = pollInterval ?? TimeSpan.FromHours(1);
        _cts = new CancellationTokenSource();

        _logger.LogInformation(
            "RefreshWorker created with poll interval of {Interval}",
            _pollInterval);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_isStarted)
            {
                _logger.LogWarning("RefreshWorker already started, ignoring duplicate StartAsync call");
                return Task.CompletedTask;
            }

            _isStarted = true;
        }

        _logger.LogInformation("RefreshWorker starting");

        // Perform initial refresh on startup (don't wait for first interval)
        _ = PerformRefreshAsync();

        // Schedule periodic refresh
        _timer = new Timer(
            OnTimerElapsed,
            null,
            _pollInterval,
            _pollInterval);

        _logger.LogInformation(
            "RefreshWorker started - initial refresh triggered, timer set for {Interval}",
            _pollInterval);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_isStopped)
            {
                _logger.LogWarning("RefreshWorker already stopped, ignoring duplicate StopAsync call");
                return;
            }

            _isStopped = true;
        }

        _logger.LogInformation("RefreshWorker stopping");

        // Stop the timer first
        _timer?.Change(Timeout.Infinite, 0);
        _timer?.Dispose();
        _timer = null;

        // Signal cancellation to any running operations
        _cts.Cancel();

        // Wait for any in-flight refresh to complete
        Task? taskToWait;
        lock (_lock)
        {
            taskToWait = _currentRefreshTask;
        }

        if (taskToWait != null)
        {
            _logger.LogInformation("Waiting for in-flight refresh to complete");
            try
            {
                await taskToWait;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("In-flight refresh was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "In-flight refresh completed with error during shutdown");
            }
        }

        _logger.LogInformation("RefreshWorker stopped");
    }

    /// <summary>
    /// Handles the timer elapsed event by triggering a refresh.
    /// </summary>
    private void OnTimerElapsed(object? state)
    {
        _ = PerformRefreshAsync();
    }

    /// <summary>
    /// Performs a refresh of all channels with error isolation.
    /// </summary>
    private async Task PerformRefreshAsync()
    {
        // Check if already running to prevent overlapping refreshes
        lock (_lock)
        {
            if (_currentRefreshTask != null && !_currentRefreshTask.IsCompleted)
            {
                _logger.LogWarning("Refresh already in progress, skipping this cycle");
                return;
            }
        }

        if (_cts.Token.IsCancellationRequested)
        {
            _logger.LogDebug("Refresh cancelled due to shutdown");
            return;
        }

        var refreshTask = RefreshAllChannelsInternalAsync();
        
        lock (_lock)
        {
            _currentRefreshTask = refreshTask;
        }

        try
        {
            await refreshTask;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Refresh was cancelled");
        }
        catch (Exception ex)
        {
            // Log error but don't crash the worker - it will retry on next interval
            _logger.LogError(ex, "Error during periodic refresh");
        }
    }

    /// <summary>
    /// Internal method to refresh all channels with error isolation per channel.
    /// The IChannelRefreshService.RefreshAllChannelsAsync already implements error isolation,
    /// so we just call it and handle top-level exceptions.
    /// </summary>
    private async Task RefreshAllChannelsInternalAsync()
    {
        var startTime = DateTimeOffset.UtcNow;
        _logger.LogInformation("Starting periodic channel refresh");

        try
        {
            // Create a scope to resolve scoped services
            using var scope = _scopeFactory.CreateScope();
            var refreshService = scope.ServiceProvider.GetRequiredService<IChannelRefreshService>();

            var results = await refreshService.RefreshAllChannelsAsync(_cts.Token);
            var resultsList = results.ToList();

            var duration = DateTimeOffset.UtcNow - startTime;
            var totalEpisodesQueued = resultsList.Sum(r => r.EpisodesQueued);
            var failedChannels = resultsList.Count(r => r.ErrorMessage != null);

            _logger.LogInformation(
                "Channel refresh complete in {Duration:mm\\:ss}. " +
                "Total channels: {ChannelCount}, Episodes queued: {EpisodesQueued}, Failures: {Failures}",
                duration,
                resultsList.Count,
                totalEpisodesQueued,
                failedChannels);

            // Log details for any failed channels
            foreach (var result in resultsList.Where(r => r.ErrorMessage != null))
            {
                _logger.LogWarning(
                    "Channel {ChannelId} refresh failed: {Error}",
                    result.ChannelId,
                    result.ErrorMessage);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Channel refresh cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during channel refresh");
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _timer?.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}