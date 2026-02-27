namespace Yallarhorn.Services;

using Yallarhorn.Data.Enums;
using Yallarhorn.Models;

/// <summary>
/// Service for generating RSS/Atom podcast feeds per channel.
/// </summary>
public interface IFeedService
{
    /// <summary>
    /// Generates a feed for a specific channel.
    /// </summary>
    /// <param name="channelId">The channel ID to generate the feed for.</param>
    /// <param name="feedType">The type of feed (audio, video, or both).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A feed generation result, or null if channel not found.</returns>
    Task<FeedGenerationResult?> GenerateFeedAsync(
        string channelId,
        FeedType feedType,
        CancellationToken cancellationToken = default);
}