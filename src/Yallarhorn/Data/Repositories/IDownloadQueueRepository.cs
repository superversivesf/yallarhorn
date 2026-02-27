using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;

namespace Yallarhorn.Data.Repositories;

/// <summary>
/// Repository interface for DownloadQueue entities.
/// </summary>
public interface IDownloadQueueRepository : IRepository<DownloadQueue>
{
    /// <summary>
    /// Gets a queue item by episode ID.
    /// </summary>
    /// <param name="episodeId">The episode ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The queue item or null.</returns>
    Task<DownloadQueue?> GetByEpisodeIdAsync(string episodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending queue items.
    /// </summary>
    /// <param name="limit">Optional limit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending queue items.</returns>
    Task<IEnumerable<DownloadQueue>> GetPendingAsync(
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets queue items by status.
    /// </summary>
    /// <param name="status">The queue status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Queue items with the status.</returns>
    Task<IEnumerable<DownloadQueue>> GetByStatusAsync(
        QueueStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets queue items ready for retry.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Queue items ready for retry.</returns>
    Task<IEnumerable<DownloadQueue>> GetReadyForRetryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts pending queue items.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count.</returns>
    Task<int> CountPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts queue items by status.
    /// </summary>
    /// <param name="status">The queue status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count.</returns>
    Task<int> CountByStatusAsync(QueueStatus status, CancellationToken cancellationToken = default);
}