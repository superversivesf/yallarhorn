using Microsoft.EntityFrameworkCore;
using Yallarhorn.Data.Entities;

namespace Yallarhorn.Data.Repositories;

/// <summary>
/// Repository implementation for Channel entities.
/// </summary>
public class ChannelRepository : Repository<Channel>, IChannelRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public ChannelRepository(YallarhornDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<Channel?> GetByUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Episodes)
            .FirstOrDefaultAsync(c => c.Url == url, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Channel>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(c => c.Enabled)
            .OrderBy(c => c.Title)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Channel>> GetChannelsNeedingRefreshAsync(
        TimeSpan minRefreshInterval,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - minRefreshInterval;
        
        var channels = await DbSet
            .Where(c => c.Enabled)
            .ToListAsync(cancellationToken);
        
        return channels
            .Where(c => c.LastRefreshAt == null || c.LastRefreshAt < cutoff)
            .OrderBy(c => c.LastRefreshAt);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(c => c.Url == url, cancellationToken);
    }
}