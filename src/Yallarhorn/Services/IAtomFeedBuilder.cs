namespace Yallarhorn.Services;

using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;

/// <summary>
/// Interface for building Atom 1.0 compliant feeds.
/// </summary>
public interface IAtomFeedBuilder
{
    /// <summary>
    /// Builds an Atom 1.0 feed XML string from a channel and episodes.
    /// </summary>
    /// <param name="channel">The channel to create the feed for.</param>
    /// <param name="episodes">The episodes to include in the feed (will be filtered by feedType).</param>
    /// <param name="feedType">The type of feed (audio, video, or both).</param>
    /// <param name="baseUrl">The base URL for constructing media file URLs.</param>
    /// <param name="feedPath">The feed path prefix (e.g., "/feeds").</param>
    /// <param name="feedUrl">The URL of the Atom feed itself (used as feed ID).</param>
    /// <returns>A complete Atom 1.0 XML string.</returns>
    string BuildAtomFeed(Channel channel, IEnumerable<Episode> episodes, FeedType feedType, string baseUrl, string feedPath, string? feedUrl = null);
}