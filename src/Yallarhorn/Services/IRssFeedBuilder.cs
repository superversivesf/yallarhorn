namespace Yallarhorn.Services;

using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;

/// <summary>
/// Interface for building RSS 2.0 compliant podcast feeds.
/// </summary>
public interface IRssFeedBuilder
{
    /// <summary>
    /// Builds an RSS 2.0 feed XML string from a channel and episodes.
    /// </summary>
    /// <param name="channel">The channel to create the feed for.</param>
    /// <param name="episodes">The episodes to include in the feed (will be filtered by feedType).</param>
    /// <param name="feedType">The type of feed (audio, video, or both).</param>
    /// <param name="baseUrl">The base URL for constructing media file URLs.</param>
    /// <param name="feedPath">The feed path prefix (e.g., "/feeds").</param>
    /// <returns>A complete RSS 2.0 XML string.</returns>
    string BuildRssFeed(Channel channel, IEnumerable<Episode> episodes, FeedType feedType, string baseUrl, string feedPath);
}