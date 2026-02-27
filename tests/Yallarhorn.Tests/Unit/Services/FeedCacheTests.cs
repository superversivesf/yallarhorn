namespace Yallarhorn.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;
using Yallarhorn.Models;
using Yallarhorn.Services;

public class FeedCacheTests : IDisposable
{
    private readonly FeedCache _cache;
    private readonly MemoryCache _memoryCache;

    public FeedCacheTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _cache = new FeedCache(_memoryCache);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenKeyNotInCache()
    {
        // Act
        var result = await _cache.GetAsync("nonexistent-key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ShouldReturnCachedResult_WhenKeyExists()
    {
        // Arrange
        var feedResult = new FeedGenerationResult
        {
            XmlContent = "<rss>test</rss>",
            Etag = "abc123",
            LastModified = DateTimeOffset.UtcNow
        };
        await _cache.SetAsync("test-key", feedResult, TimeSpan.FromMinutes(5));

        // Act
        var result = await _cache.GetAsync("test-key");

        // Assert
        result.Should().NotBeNull();
        result!.XmlContent.Should().Be("<rss>test</rss>");
        result.Etag.Should().Be("abc123");
    }

    #endregion

    #region SetAsync Tests

    [Fact]
    public async Task SetAsync_ShouldStoreFeedResultInCache()
    {
        // Arrange
        var feedResult = new FeedGenerationResult
        {
            XmlContent = "<rss>content</rss>",
            Etag = "etag-value",
            LastModified = DateTimeOffset.UtcNow
        };

        // Act
        await _cache.SetAsync("new-key", feedResult, TimeSpan.FromMinutes(5));

        // Assert
        var stored = await _cache.GetAsync("new-key");
        stored.Should().NotBeNull();
        stored!.XmlContent.Should().Be("<rss>content</rss>");
    }

    [Fact]
    public async Task SetAsync_ShouldOverwriteExistingValue()
    {
        // Arrange
        var original = new FeedGenerationResult
        {
            XmlContent = "<rss>original</rss>",
            Etag = "etag1",
            LastModified = DateTimeOffset.UtcNow
        };
        var updated = new FeedGenerationResult
        {
            XmlContent = "<rss>updated</rss>",
            Etag = "etag2",
            LastModified = DateTimeOffset.UtcNow
        };

        await _cache.SetAsync("same-key", original, TimeSpan.FromMinutes(5));

        // Act
        await _cache.SetAsync("same-key", updated, TimeSpan.FromMinutes(5));

        // Assert
        var result = await _cache.GetAsync("same-key");
        result!.XmlContent.Should().Be("<rss>updated</rss>");
        result.Etag.Should().Be("etag2");
    }

    #endregion

    #region GetOrCreateAsync Tests

    [Fact]
    public async Task GetOrCreateAsync_ShouldCallFactory_WhenKeyNotInCache()
    {
        // Arrange
        var factoryCallCount = 0;
        Task<FeedGenerationResult> Factory()
        {
            factoryCallCount++;
            return Task.FromResult(new FeedGenerationResult
            {
                XmlContent = "<rss>generated</rss>",
                Etag = "generated-etag",
                LastModified = DateTimeOffset.UtcNow
            });
        }

        // Act
        var result = await _cache.GetOrCreateAsync("channel-123:audio", Factory);

        // Assert
        result.Should().NotBeNull();
        result.XmlContent.Should().Be("<rss>generated</rss>");
        factoryCallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_ShouldReturnCachedValue_WhenKeyExists()
    {
        // Arrange
        var cachedResult = new FeedGenerationResult
        {
            XmlContent = "<rss>cached</rss>",
            Etag = "cached-etag",
            LastModified = DateTimeOffset.UtcNow
        };
        await _cache.SetAsync("channel-123:audio", cachedResult, TimeSpan.FromMinutes(5));

        var factoryCallCount = 0;
        Task<FeedGenerationResult> Factory()
        {
            factoryCallCount++;
            return Task.FromResult(new FeedGenerationResult
            {
                XmlContent = "<rss>new-generated</rss>",
                Etag = "new-etag",
                LastModified = DateTimeOffset.UtcNow
            });
        }

        // Act
        var result = await _cache.GetOrCreateAsync("channel-123:audio", Factory);

        // Assert
        result.XmlContent.Should().Be("<rss>cached</rss>");
        result.Etag.Should().Be("cached-etag");
        factoryCallCount.Should().Be(0); // Factory should not be called
    }

    [Fact]
    public async Task GetOrCreateAsync_ShouldCacheTheResultFromFactory()
    {
        // Arrange
        var factoryCallCount = 0;
        Task<FeedGenerationResult> Factory()
        {
            factoryCallCount++;
            return Task.FromResult(new FeedGenerationResult
            {
                XmlContent = "<rss>first-call</rss>",
                Etag = "etag1",
                LastModified = DateTimeOffset.UtcNow
            });
        }

        // Act - First call creates, second call gets cached
        var result1 = await _cache.GetOrCreateAsync("test-channel:video", Factory);
        var result2 = await _cache.GetOrCreateAsync("test-channel:video", Factory);

        // Assert
        factoryCallCount.Should().Be(1); // Factory called only once
        result1.XmlContent.Should().Be(result2.XmlContent);
    }

    [Fact]
    public async Task GetOrCreateAsync_ShouldWorkForCombinedFeedKey()
    {
        // Arrange
        var factoryCallCount = 0;
        Task<FeedGenerationResult> Factory()
        {
            factoryCallCount++;
            return Task.FromResult(new FeedGenerationResult
            {
                XmlContent = "<rss>combined</rss>",
                Etag = "combined-etag",
                LastModified = DateTimeOffset.UtcNow
            });
        }

        // Act
        var result = await _cache.GetOrCreateAsync("combined:audio", Factory);

        // Assert
        result.Should().NotBeNull();
        result.XmlContent.Should().Be("<rss>combined</rss>");
        factoryCallCount.Should().Be(1);
    }

    #endregion

    #region InvalidateChannelAsync Tests

    [Fact]
    public async Task InvalidateChannelAsync_ShouldRemoveAllFeedTypesForChannel()
    {
        // Arrange
        var channelResult = new FeedGenerationResult
        {
            XmlContent = "<rss>channel-feed</rss>",
            Etag = "etag1",
            LastModified = DateTimeOffset.UtcNow
        };

        await _cache.SetAsync("channel-123:audio", channelResult, TimeSpan.FromMinutes(5));
        await _cache.SetAsync("channel-123:video", channelResult, TimeSpan.FromMinutes(5));
        await _cache.SetAsync("channel-456:audio", channelResult, TimeSpan.FromMinutes(5)); // Different channel

        // Act
        await _cache.InvalidateChannelAsync("channel-123");

        // Assert
        var audioResult = await _cache.GetAsync("channel-123:audio");
        var videoResult = await _cache.GetAsync("channel-123:video");
        var otherChannelResult = await _cache.GetAsync("channel-456:audio");

        audioResult.Should().BeNull();
        videoResult.Should().BeNull();
        otherChannelResult.Should().NotBeNull(); // Other channel unaffected
    }

    [Fact]
    public async Task InvalidateChannelAsync_ShouldNotAffectOtherChannels()
    {
        // Arrange
        var feedResult = new FeedGenerationResult
        {
            XmlContent = "<rss>test</rss>",
            Etag = "etag",
            LastModified = DateTimeOffset.UtcNow
        };

        await _cache.SetAsync("channel-a:audio", feedResult, TimeSpan.FromMinutes(5));
        await _cache.SetAsync("channel-b:audio", feedResult, TimeSpan.FromMinutes(5));

        // Act
        await _cache.InvalidateChannelAsync("channel-a");

        // Assert
        var channelAResult = await _cache.GetAsync("channel-a:audio");
        var channelBResult = await _cache.GetAsync("channel-b:audio");

        channelAResult.Should().BeNull();
        channelBResult.Should().NotBeNull();
    }

    [Fact]
    public async Task InvalidateChannelAsync_ShouldNotAffectCombinedFeeds()
    {
        // Arrange
        var feedResult = new FeedGenerationResult
        {
            XmlContent = "<rss>combined</rss>",
            Etag = "etag",
            LastModified = DateTimeOffset.UtcNow
        };

        await _cache.SetAsync("combined:audio", feedResult, TimeSpan.FromMinutes(5));

        // Act
        await _cache.InvalidateChannelAsync("some-channel");

        // Assert
        var combinedResult = await _cache.GetAsync("combined:audio");
        combinedResult.Should().NotBeNull();
    }

    #endregion

    #region InvalidateAllAsync Tests

    [Fact]
    public async Task InvalidateAllAsync_ShouldClearEntireCache()
    {
        // Arrange
        var feedResult = new FeedGenerationResult
        {
            XmlContent = "<rss>test</rss>",
            Etag = "etag",
            LastModified = DateTimeOffset.UtcNow
        };

        await _cache.SetAsync("channel-1:audio", feedResult, TimeSpan.FromMinutes(5));
        await _cache.SetAsync("channel-1:video", feedResult, TimeSpan.FromMinutes(5));
        await _cache.SetAsync("channel-2:audio", feedResult, TimeSpan.FromMinutes(5));
        await _cache.SetAsync("combined:audio", feedResult, TimeSpan.FromMinutes(5));

        // Act
        await _cache.InvalidateAllAsync();

        // Assert
        var allKeys = new[] { "channel-1:audio", "channel-1:video", "channel-2:audio", "combined:audio" };
        foreach (var key in allKeys)
        {
            var result = await _cache.GetAsync(key);
            result.Should().BeNull($"Key '{key}' should be cleared");
        }
    }

    #endregion

    #region Cache Expiration Tests

    [Fact]
    public async Task Cache_ShouldExpireAfterDuration()
    {
        // Arrange
        var feedResult = new FeedGenerationResult
        {
            XmlContent = "<rss>expiring</rss>",
            Etag = "etag",
            LastModified = DateTimeOffset.UtcNow
        };

        // Use very short expiration
        await _cache.SetAsync("expiring-key", feedResult, TimeSpan.FromMilliseconds(100));

        // Act - Wait for expiration
        await Task.Delay(150);

        // Assert
        var result = await _cache.GetAsync("expiring-key");
        result.Should().BeNull("Cache entry should have expired");
    }

    #endregion

    #region ETag Generation Tests

    [Fact]
    public async Task GetOrCreateAsync_ShouldPreserveETagFromFactory()
    {
        // Arrange
        Task<FeedGenerationResult> Factory() => Task.FromResult(new FeedGenerationResult
        {
            XmlContent = "<rss>content</rss>",
            Etag = "sha256:abc123",
            LastModified = DateTimeOffset.UtcNow
        });

        // Act
        var result = await _cache.GetOrCreateAsync("etag-test-key", Factory);

        // Assert
        result.Etag.Should().Be("sha256:abc123");
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task GetOrCreateAsync_ShouldBeThreadSafe()
    {
        // Arrange
        var factoryCallCount = 0;
        var lockObj = new object();

        async Task<FeedGenerationResult> Factory()
        {
            lock (lockObj)
            {
                factoryCallCount++;
            }
            await Task.Delay(50); // Simulate slow generation
            return new FeedGenerationResult
            {
                XmlContent = "<rss>threadsafe</rss>",
                Etag = "threadsafe-etag",
                LastModified = DateTimeOffset.UtcNow
            };
        }

        // Act - Simulate concurrent requests
        var tasks = new List<Task<FeedGenerationResult>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_cache.GetOrCreateAsync("concurrent-key", Factory));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All results should be the same
        results.Should().AllSatisfy(r => r.XmlContent.Should().Be("<rss>threadsafe</rss>"));
        results.Should().AllSatisfy(r => r.Etag.Should().Be("threadsafe-etag"));
        
        // Note: IMemoryCache doesn't guarantee single factory invocation
        // This test verifies thread-safety (no crashes), not single invocation
    }

    #endregion
}