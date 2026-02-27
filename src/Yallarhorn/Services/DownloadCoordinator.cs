namespace Yallarhorn.Services;

using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for download concurrency coordination.
/// Manages concurrent download slots using semaphores.
/// </summary>
public interface IDownloadCoordinator : IDisposable
{
    /// <summary>
    /// Gets the maximum number of concurrent downloads allowed.
    /// </summary>
    int MaxConcurrentDownloads { get; }

    /// <summary>
    /// Gets the current number of active downloads.
    /// </summary>
    int ActiveDownloads { get; }

    /// <summary>
    /// Gets the number of available download slots.
    /// </summary>
    int AvailableSlots { get; }

    /// <summary>
    /// Acquires a download slot, waiting if necessary.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when a slot is acquired.</returns>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the coordinator has been disposed.</exception>
    Task AcquireSlotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a previously acquired download slot.
    /// </summary>
    void ReleaseSlot();

    /// <summary>
    /// Executes a download operation with automatic slot management.
    /// Acquires a slot, executes the operation, and releases the slot.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The download operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    Task<T> ExecuteDownloadAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a download operation with automatic slot management.
    /// Acquires a slot, executes the operation, and releases the slot.
    /// </summary>
    /// <param name="operation">The download operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the operation is done.</returns>
    Task ExecuteDownloadAsync(Func<Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a download operation with automatic slot management and cancellation support.
    /// Acquires a slot, executes the operation, and releases the slot.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The download operation with cancellation support.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    Task<T> ExecuteDownloadAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}

/// <summary>
/// Coordinates concurrent downloads using a semaphore-based approach.
/// </summary>
public class DownloadCoordinator : IDownloadCoordinator
{
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger<DownloadCoordinator> _logger;
    private readonly int _maxConcurrent;
    private int _disposed = 0;

    /// <summary>
    /// Gets the maximum number of concurrent downloads allowed.
    /// </summary>
    public int MaxConcurrentDownloads => _maxConcurrent;

    /// <summary>
    /// Gets the current number of active downloads.
    /// </summary>
    public int ActiveDownloads => _maxConcurrent - _semaphore.CurrentCount;

    /// <summary>
    /// Gets the number of available download slots.
    /// </summary>
    public int AvailableSlots => _semaphore.CurrentCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadCoordinator"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="maxConcurrentDownloads">Maximum concurrent downloads (default: 3).</param>
    public DownloadCoordinator(ILogger<DownloadCoordinator> logger, int maxConcurrentDownloads = 3)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Ensure minimum of 1 concurrent download
        _maxConcurrent = Math.Max(1, maxConcurrentDownloads);
        _semaphore = new SemaphoreSlim(_maxConcurrent, _maxConcurrent);

        _logger.LogInformation(
            "Download coordinator initialized with {MaxConcurrent} concurrent slots",
            _maxConcurrent);
    }

    /// <inheritdoc />
    public async Task AcquireSlotAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _logger.LogDebug("Waiting for download slot (available: {Available})", AvailableSlots);

        try
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "Download slot acquired (active: {Active}, available: {Available})",
                ActiveDownloads,
                AvailableSlots);
        }
        catch (ObjectDisposedException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error acquiring download slot");
            throw;
        }
    }

    /// <inheritdoc />
    public void ReleaseSlot()
    {
        if (_disposed == 1)
        {
            // Silently return if disposed - no need to throw for release
            return;
        }

        try
        {
            _semaphore.Release();

            _logger.LogDebug(
                "Download slot released (active: {Active}, available: {Available})",
                ActiveDownloads,
                AvailableSlots);
        }
        catch (SemaphoreFullException ex)
        {
            // This shouldn't happen in normal operation, but log it if it does
            _logger.LogWarning(ex, "Attempted to release more slots than acquired");
        }
    }

    /// <inheritdoc />
    public async Task<T> ExecuteDownloadAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await AcquireSlotAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await operation().ConfigureAwait(false);
            return result;
        }
        finally
        {
            ReleaseSlot();
        }
    }

    /// <inheritdoc />
    public async Task ExecuteDownloadAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await AcquireSlotAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await operation().ConfigureAwait(false);
        }
        finally
        {
            ReleaseSlot();
        }
    }

    /// <summary>
    /// Executes a download operation with automatic slot management and cancellation support.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The download operation with cancellation support.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public async Task<T> ExecuteDownloadAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await AcquireSlotAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await operation(cancellationToken).ConfigureAwait(false);
            return result;
        }
        finally
        {
            ReleaseSlot();
        }
    }

    /// <summary>
    /// Throws an ObjectDisposedException if the coordinator has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed == 1)
        {
            throw new ObjectDisposedException(nameof(DownloadCoordinator));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _semaphore.Dispose();

        _logger.LogInformation("Download coordinator disposed");

        GC.SuppressFinalize(this);
    }
}