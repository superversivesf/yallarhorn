namespace Yallarhorn.Services;

using Yallarhorn.Models;

/// <summary>
/// Service for caching generated feed content with cache invalidation support.
/// </summary>
public interface IFeedCache
{
    /// <summary>
    /// Gets a cached feed result by key, or null if not found.
    /// </summary>
    /// <param name="key">The cache key (e.g., "channel-123:audio" or "combined:video").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached FeedGenerationResult, or null if not in cache.</returns>
    Task<FeedGenerationResult?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a feed result in the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="result">The feed result to cache.</param>
    /// <param name="expiry">Time until the cache entry expires.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync(string key, FeedGenerationResult result, TimeSpan expiry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a cached feed result, or creates and caches it using the factory function.
    /// For per-channel feeds, use: "{channelId}:{feedType}"
    /// For combined feeds, use: "combined:{feedType}"
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">Factory function to create the result if not cached.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached or newly created feed result.</returns>
    Task<FeedGenerationResult> GetOrCreateAsync(string key, Func<Task<FeedGenerationResult>> factory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all cached feed entries for a specific channel.
    /// Removes entries like "channel-123:audio" and "channel-123:video".
    /// </summary>
    /// <param name="channelId">The channel ID to invalidate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateChannelAsync(string channelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateAllAsync(CancellationToken cancellationToken = default);
}