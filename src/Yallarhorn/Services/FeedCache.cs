namespace Yallarhorn.Services;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Yallarhorn.Models;

/// <summary>
/// In-memory cache for generated feed content with channel-based invalidation support.
/// </summary>
public class FeedCache : IFeedCache
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _defaultCacheDuration;
    private readonly HashSet<string> _trackedKeys = new();
    private readonly object _keyLock = new();

    /// <summary>
    /// Prefix for channel-specific cache keys.
    /// </summary>
    public const string ChannelKeyPrefix = "feed:channel:";

    /// <summary>
    /// Prefix for combined feed cache keys.
    /// </summary>
    public const string CombinedKeyPrefix = "feed:combined:";

    /// <summary>
    /// Creates a new FeedCache instance.
    /// </summary>
    /// <param name="cache">The memory cache instance.</param>
    /// <param name="defaultCacheDuration">Optional default cache duration. Defaults to 5 minutes.</param>
    public FeedCache(IMemoryCache cache, TimeSpan? defaultCacheDuration = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _defaultCacheDuration = defaultCacheDuration ?? TimeSpan.FromMinutes(5);
    }

    /// <inheritdoc/>
    public Task<FeedGenerationResult?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var fullKey = GetFullKey(key);
        var result = _cache.Get<FeedGenerationResult>(fullKey);

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task SetAsync(string key, FeedGenerationResult result, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(result);

        var fullKey = GetFullKey(key);

        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry,
            SlidingExpiration = TimeSpan.FromMinutes(2) // Allow sliding expiration for frequently accessed feeds
        };

        // Track the key for invalidation purposes
        TrackKey(key);

        _cache.Set(fullKey, result, cacheEntryOptions);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<FeedGenerationResult> GetOrCreateAsync(
        string key, 
        Func<Task<FeedGenerationResult>> factory, 
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(factory);

        var fullKey = GetFullKey(key);

        // Check if already cached
        if (_cache.TryGetValue(fullKey, out FeedGenerationResult? cachedResult) && cachedResult != null)
        {
            return cachedResult;
        }

        // Create new entry
        var result = await factory();

        // Cache the result
        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _defaultCacheDuration,
            SlidingExpiration = TimeSpan.FromMinutes(2)
        };

        // Track the key for invalidation purposes
        TrackKey(key);

        _cache.Set(fullKey, result, cacheEntryOptions);

        return result;
    }

    /// <inheritdoc/>
    public Task InvalidateChannelAsync(string channelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(channelId);

        // Get all known feed types for this channel
        var feedTypes = new[] { "audio", "video" };

        foreach (var feedType in feedTypes)
        {
            var key = $"{ChannelKeyPrefix}{channelId}:{feedType}";
            _cache.Remove(key);
        }

        // Also remove from tracked keys
        lock (_keyLock)
        {
            _trackedKeys.RemoveWhere(k => k.StartsWith($"{channelId}:"));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        // Remove all tracked keys
        lock (_keyLock)
        {
            foreach (var key in _trackedKeys)
            {
                var fullKey = GetFullKey(key);
                _cache.Remove(fullKey);
            }

            _trackedKeys.Clear();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates a cache key for a channel's specific feed type.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="feedType">The feed type (audio, video, etc.).</param>
    /// <returns>The formatted cache key.</returns>
    public static string GetChannelKey(string channelId, string feedType)
    {
        return $"{channelId}:{feedType}";
    }

    /// <summary>
    /// Generates a cache key for the combined feed.
    /// </summary>
    /// <param name="feedType">The feed type (audio, video, etc.).</param>
    /// <returns>The formatted cache key.</returns>
    public static string GetCombinedKey(string feedType)
    {
        return $"combined:{feedType}";
    }

    private static string GetFullKey(string key)
    {
        // Determine prefix based on key format
        if (key.StartsWith("combined:"))
        {
            return CombinedKeyPrefix + key.Substring("combined:".Length);
        }

        // For channel keys: "channelId:feedType" format
        return ChannelKeyPrefix + key;
    }

    private void TrackKey(string key)
    {
        lock (_keyLock)
        {
            // Store without prefix for easier lookup
            // Extract channel ID from key like "channelId:audio"
            _trackedKeys.Add(key);
        }
    }
}