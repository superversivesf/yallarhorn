using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;

namespace Yallarhorn.Data.Repositories;

/// <summary>
/// Repository interface for Episode entities.
/// </summary>
public interface IEpisodeRepository : IRepository<Episode>
{
    /// <summary>
    /// Gets an episode by its YouTube video ID.
    /// </summary>
    /// <param name="videoId">The YouTube video ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The episode or null.</returns>
    Task<Episode?> GetByVideoIdAsync(string videoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets episodes by channel ID.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="limit">Optional limit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Episodes for the channel.</returns>
    Task<IEnumerable<Episode>> GetByChannelIdAsync(
        string channelId,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets episodes by status.
    /// </summary>
    /// <param name="status">The episode status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Episodes with the status.</returns>
    Task<IEnumerable<Episode>> GetByStatusAsync(
        EpisodeStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets downloaded episodes for a channel.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="feedType">The feed type (audio/video/both).</param>
    /// <param name="limit">Maximum number to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Downloaded episodes.</returns>
    Task<IEnumerable<Episode>> GetDownloadedAsync(
        string channelId,
        FeedType feedType,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts episodes for a channel.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count.</returns>
    Task<int> CountByChannelIdAsync(string channelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the oldest episodes for a channel.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="count">Number to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Oldest episodes.</returns>
    Task<IEnumerable<Episode>> GetOldestByChannelIdAsync(
        string channelId,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an episode exists by video ID.
    /// </summary>
    /// <param name="videoId">The YouTube video ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if exists.</returns>
    Task<bool> ExistsByVideoIdAsync(string videoId, CancellationToken cancellationToken = default);
}