using Microsoft.EntityFrameworkCore;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;

namespace Yallarhorn.Data.Repositories;

/// <summary>
/// Repository implementation for DownloadQueue entities.
/// </summary>
public class DownloadQueueRepository : Repository<DownloadQueue>, IDownloadQueueRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadQueueRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public DownloadQueueRepository(YallarhornDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<DownloadQueue?> GetByEpisodeIdAsync(
        string episodeId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(dq => dq.Episode)
            .FirstOrDefaultAsync(dq => dq.EpisodeId == episodeId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DownloadQueue>> GetPendingAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var items = await DbSet
            .Where(dq => dq.Status == QueueStatus.Pending)
            .ToListAsync(cancellationToken);

        var ordered = items.OrderBy(dq => dq.Priority).ThenBy(dq => dq.CreatedAt);
        return limit.HasValue ? ordered.Take(limit.Value) : ordered;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DownloadQueue>> GetByStatusAsync(
        QueueStatus status,
        CancellationToken cancellationToken = default)
    {
        var items = await DbSet
            .Where(dq => dq.Status == status)
            .ToListAsync(cancellationToken);
        
        return items.OrderBy(dq => dq.Priority).ThenBy(dq => dq.CreatedAt);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DownloadQueue>> GetReadyForRetryAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        
        var items = await DbSet
            .Where(dq => dq.Status == QueueStatus.Retrying && dq.NextRetryAt != null)
            .ToListAsync(cancellationToken);
        
        return items
            .Where(dq => dq.NextRetryAt <= now)
            .OrderBy(dq => dq.Priority)
            .ThenBy(dq => dq.NextRetryAt);
    }

    /// <inheritdoc />
    public async Task<int> CountPendingAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.CountAsync(dq => dq.Status == QueueStatus.Pending, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CountByStatusAsync(QueueStatus status, CancellationToken cancellationToken = default)
    {
        return await DbSet.CountAsync(dq => dq.Status == status, cancellationToken);
    }
}