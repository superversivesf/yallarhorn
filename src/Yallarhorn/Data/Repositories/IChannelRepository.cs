using Yallarhorn.Data.Entities;

namespace Yallarhorn.Data.Repositories;

/// <summary>
/// Repository interface for Channel entities.
/// </summary>
public interface IChannelRepository : IRepository<Channel>
{
    /// <summary>
    /// Gets a channel by its URL.
    /// </summary>
    /// <param name="url">The channel URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The channel or null.</returns>
    Task<Channel?> GetByUrlAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all enabled channels.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enabled channels.</returns>
    Task<IEnumerable<Channel>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets channels that need refresh based on their last refresh time.
    /// </summary>
    /// <param name="minRefreshInterval">Minimum time since last refresh.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Channels needing refresh.</returns>
    Task<IEnumerable<Channel>> GetChannelsNeedingRefreshAsync(
        TimeSpan minRefreshInterval,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a channel exists by URL.
    /// </summary>
    /// <param name="url">The channel URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if exists.</returns>
    Task<bool> ExistsByUrlAsync(string url, CancellationToken cancellationToken = default);
}