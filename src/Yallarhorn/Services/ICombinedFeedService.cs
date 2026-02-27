namespace Yallarhorn.Services;

using Yallarhorn.Data.Enums;
using Yallarhorn.Models;

/// <summary>
/// Service for generating combined RSS/Atom podcast feeds from all enabled channels.
/// </summary>
public interface ICombinedFeedService
{
    /// <summary>
    /// Generates a combined feed from all enabled channels.
    /// </summary>
    /// <param name="feedType">The type of feed (audio, video, or both).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A feed generation result with aggregated episodes from all channels.</returns>
    Task<FeedGenerationResult> GenerateCombinedFeedAsync(
        FeedType feedType,
        CancellationToken cancellationToken = default);
}