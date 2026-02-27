using Microsoft.EntityFrameworkCore;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;

namespace Yallarhorn.Data.Repositories;

/// <summary>
/// Repository implementation for Episode entities.
/// </summary>
public class EpisodeRepository : Repository<Episode>, IEpisodeRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public EpisodeRepository(YallarhornDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<Episode?> GetByVideoIdAsync(string videoId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(e => e.Channel)
            .FirstOrDefaultAsync(e => e.VideoId == videoId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Episode>> GetByChannelIdAsync(
        string channelId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var episodes = await DbSet
            .Where(e => e.ChannelId == channelId)
            .ToListAsync(cancellationToken);

        var ordered = episodes.OrderByDescending(e => e.PublishedAt);
        return limit.HasValue ? ordered.Take(limit.Value) : ordered;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Episode>> GetByStatusAsync(
        EpisodeStatus status,
        CancellationToken cancellationToken = default)
    {
        var episodes = await DbSet
            .Where(e => e.Status == status)
            .ToListAsync(cancellationToken);
        
        return episodes.OrderBy(e => e.CreatedAt);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Episode>> GetDownloadedAsync(
        string channelId,
        FeedType feedType,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .Where(e => e.ChannelId == channelId && e.Status == EpisodeStatus.Completed);

        query = feedType switch
        {
            FeedType.Audio => query.Where(e => e.FilePathAudio != null),
            FeedType.Video => query.Where(e => e.FilePathVideo != null),
            FeedType.Both => query.Where(e => e.FilePathAudio != null || e.FilePathVideo != null),
            _ => query
        };

        var episodes = await query.ToListAsync(cancellationToken);
        return episodes.OrderByDescending(e => e.PublishedAt).Take(limit);
    }

    /// <inheritdoc />
    public async Task<int> CountByChannelIdAsync(string channelId, CancellationToken cancellationToken = default)
    {
        return await DbSet.CountAsync(e => e.ChannelId == channelId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Episode>> GetOldestByChannelIdAsync(
        string channelId,
        int count,
        CancellationToken cancellationToken = default)
    {
        var episodes = await DbSet
            .Where(e => e.ChannelId == channelId)
            .ToListAsync(cancellationToken);
        
        return episodes.OrderBy(e => e.PublishedAt).Take(count);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByVideoIdAsync(string videoId, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(e => e.VideoId == videoId, cancellationToken);
    }
}