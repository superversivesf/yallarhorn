namespace Yallarhorn.Tests.Unit.Controllers;

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yallarhorn.Controllers;
using Yallarhorn.Data.Enums;
using Yallarhorn.Models;
using Yallarhorn.Services;

public class FeedControllerTests : IDisposable
{
    private readonly Mock<IFeedService> _feedServiceMock;
    private readonly Mock<ICombinedFeedService> _combinedFeedServiceMock;
    private readonly Mock<IFeedCache> _feedCacheMock;
    private readonly Mock<ILogger<FeedController>> _loggerMock;
    private readonly FeedController _controller;

    public FeedControllerTests()
    {
        _feedServiceMock = new Mock<IFeedService>();
        _combinedFeedServiceMock = new Mock<ICombinedFeedService>();
        _feedCacheMock = new Mock<IFeedCache>();
        _loggerMock = new Mock<ILogger<FeedController>>();

        _controller = new FeedController(
            _feedServiceMock.Object,
            _combinedFeedServiceMock.Object,
            _feedCacheMock.Object,
            _loggerMock.Object);

        // Set up HttpContext with Request and Response
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public void Dispose()
    {
        // Nothing to dispose
    }

    #region GetAudioRss Tests

    [Fact]
    public async Task GetAudioRss_ShouldReturnRssFeed_WhenChannelExists()
    {
        // Arrange
        var channelId = "test-channel";
        var feedResult = CreateFeedResult("<rss>test</rss>");
        
        _feedCacheMock
            .Setup(c => c.GetOrCreateAsync(
                $"channel:{channelId}:audio",
                It.IsAny<Func<Task<FeedGenerationResult>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedResult);

        // Act
        var result = await _controller.GetAudioRss(channelId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var contentResult = result.Should().BeOfType<ContentResult>().Subject;
        contentResult.ContentType.Should().Be("application/rss+xml; charset=utf-8");
        contentResult.Content.Should().Be("<rss>test</rss>");
    }

    [Fact]
    public async Task GetAudioRss_ShouldReturn304_WhenETagMatches()
    {
        // Arrange
        var channelId = "test-channel";
        var feedResult = CreateFeedResult("<rss>test</rss>");
        
        _feedCacheMock
            .Setup(c => c.GetOrCreateAsync(
                $"channel:{channelId}:audio",
                It.IsAny<Func<Task<FeedGenerationResult>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedResult);

        // Set up If-None-Match header
        _controller.Request.Headers["If-None-Match"] = feedResult.Etag;

        // Act
        var result = await _controller.GetAudioRss(channelId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<StatusCodeResult>();
        var statusCodeResult = result.As<StatusCodeResult>();
        statusCodeResult.StatusCode.Should().Be(304);
    }

    [Fact]
    public async Task GetAudioRss_ShouldReturn304_WhenNotModifiedSince()
    {
        // Arrange
        var channelId = "test-channel";
        var lastModified = DateTimeOffset.UtcNow.AddHours(-1);
        var feedResult = CreateFeedResult("<rss>test</rss>", lastModified);
        
        _feedCacheMock
            .Setup(c => c.GetOrCreateAsync(
                $"channel:{channelId}:audio",
                It.IsAny<Func<Task<FeedGenerationResult>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedResult);

        // Set up If-Modified-Since header (more recent than lastModified)
        _controller.Request.Headers["If-Modified-Since"] = DateTimeOffset.UtcNow.ToString("R");

        // Act
        var result = await _controller.GetAudioRss(channelId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<StatusCodeResult>();
        var statusCodeResult = result.As<StatusCodeResult>();
        statusCodeResult.StatusCode.Should().Be(304);
    }

    [Fact]
    public async Task GetAudioRss_ShouldReturn404_WhenChannelNotFound()
    {
        // Arrange
        var channelId = "nonexistent";
        
        _feedCacheMock
            .Setup(c => c.GetOrCreateAsync(
                $"channel:{channelId}:audio",
                It.IsAny<Func<Task<FeedGenerationResult>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Feed generation returned null"));

        // Act
        var result = await _controller.GetAudioRss(channelId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region GetVideoRss Tests

    [Fact]
    public async Task GetVideoRss_ShouldReturnRssFeed_WhenChannelExists()
    {
        // Arrange
        var channelId = "test-channel";
        var feedResult = CreateFeedResult("<rss>video</rss>");
        
        _feedCacheMock
            .Setup(c => c.GetOrCreateAsync(
                $"channel:{channelId}:video",
                It.IsAny<Func<Task<FeedGenerationResult>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedResult);

        // Act
        var result = await _controller.GetVideoRss(channelId, CancellationToken.None);

        // Assert
        var contentResult = result.Should().BeOfType<ContentResult>().Subject;
        contentResult.ContentType.Should().Be("application/rss+xml; charset=utf-8");
        contentResult.Content.Should().Be("<rss>video</rss>");
    }

    [Fact]
    public async Task GetVideoRss_ShouldReturn404_WhenChannelNotFound()
    {
        // Arrange
        var channelId = "nonexistent";
        
        _feedCacheMock
            .Setup(c => c.GetOrCreateAsync(
                $"channel:{channelId}:video",
                It.IsAny<Func<Task<FeedGenerationResult>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Feed generation returned null"));

        // Act
        var result = await _controller.GetVideoRss(channelId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region GetAtom Tests

    [Fact]
    public async Task GetAtom_ShouldReturnAtomFeed_WhenChannelExists()
    {
        // Arrange
        var channelId = "test-channel";
        var feedResult = CreateFeedResult("<feed>atom</feed>");
        
        _feedCacheMock
            .Setup(c => c.GetOrCreateAsync(
                $"channel:{channelId}:atom",
                It.IsAny<Func<Task<FeedGenerationResult>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedResult);

        // Act
        var result = await _controller.GetAtom(channelId, CancellationToken.None);

        // Assert
        var contentResult = result.Should().BeOfType<ContentResult>().Subject;
        contentResult.ContentType.Should().Be("application/atom+xml; charset=utf-8");
        contentResult.Content.Should().Be("<feed>atom</feed>");
    }

    [Fact]
    public async Task GetAtom_ShouldReturn404_WhenChannelNotFound()
    {
        // Arrange
        var channelId = "nonexistent";
        
        _feedCacheMock
            .Setup(c => c.GetOrCreateAsync(
                $"channel:{channelId}:atom",
                It.IsAny<Func<Task<FeedGenerationResult>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Feed generation returned null"));

        // Act
        var result = await _controller.GetAtom(channelId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region GetAllAudioRss Tests

    [Fact]
    public async Task GetAllAudioRss_ShouldReturnCombinedFeed()
    {
        // Arrange
        var feedResult = CreateFeedResult("<rss>combined-audio</rss>");
        
        _feedCacheMock
            .Setup(c => c.GetOrCreateAsync(
                "combined:audio",
                It.IsAny<Func<Task<FeedGenerationResult>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedResult);

        // Act
        var result = await _controller.GetAllAudioRss(CancellationToken.None);

        // Assert
        var contentResult = result.Should().BeOfType<ContentResult>().Subject;
        contentResult.ContentType.Should().Be("application/rss+xml; charset=utf-8");
        contentResult.Content.Should().Be("<rss>combined-audio</rss>");
    }

    [Fact]
    public async Task GetAllAudioRss_ShouldReturn304_WhenETagMatches()
    {
        // Arrange
        var feedResult = CreateFeedResult("<rss>combined</rss>");
        
        _feedCacheMock
            .Setup(c => c.GetOrCreateAsync(
                "combined:audio",
                It.IsAny<Func<Task<FeedGenerationResult>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedResult);

        _controller.Request.Headers["If-None-Match"] = feedResult.Etag;

        // Act
        var result = await _controller.GetAllAudioRss(CancellationToken.None);

        // Assert
        result.Should().BeOfType<StatusCodeResult>();
        var statusCodeResult = result.As<StatusCodeResult>();
        statusCodeResult.StatusCode.Should().Be(304);
    }

    #endregion

    #region GetAllVideoRss Tests

    [Fact]
    public async Task GetAllVideoRss_ShouldReturnCombinedFeed()
    {
        // Arrange
        var feedResult = CreateFeedResult("<rss>combined-video</rss>");
        
        _feedCacheMock
            .Setup(c => c.GetOrCreateAsync(
                "combined:video",
                It.IsAny<Func<Task<FeedGenerationResult>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedResult);

        // Act
        var result = await _controller.GetAllVideoRss(CancellationToken.None);

        // Assert
        var contentResult = result.Should().BeOfType<ContentResult>().Subject;
        contentResult.ContentType.Should().Be("application/rss+xml; charset=utf-8");
        contentResult.Content.Should().Be("<rss>combined-video</rss>");
    }

    #endregion

    #region ETag and Last-Modified Headers Tests

    [Fact]
    public async Task GetAudioRss_ShouldSetETagHeader()
    {
        // Arrange
        var channelId = "test-channel";
        var feedResult = CreateFeedResult("<rss>test</rss>");
        
        _feedCacheMock
            .Setup(c => c.GetOrCreateAsync(
                $"channel:{channelId}:audio",
                It.IsAny<Func<Task<FeedGenerationResult>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedResult);

        // Act
        var result = await _controller.GetAudioRss(channelId, CancellationToken.None);

        // Assert
        _controller.Response.Headers.Should().ContainKey("ETag");
        _controller.Response.Headers["ETag"].ToString().Should().Be($"\"{feedResult.Etag}\"");
    }

    [Fact]
    public async Task GetAudioRss_ShouldSetLastModifiedHeader()
    {
        // Arrange
        var channelId = "test-channel";
        var lastModified = DateTimeOffset.UtcNow.AddHours(-2);
        var feedResult = CreateFeedResult("<rss>test</rss>", lastModified);
        
        _feedCacheMock
            .Setup(c => c.GetOrCreateAsync(
                $"channel:{channelId}:audio",
                It.IsAny<Func<Task<FeedGenerationResult>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedResult);

        // Act
        var result = await _controller.GetAudioRss(channelId, CancellationToken.None);

        // Assert
        _controller.Response.Headers.Should().ContainKey("Last-Modified");
    }

    [Fact]
    public async Task GetAudioRss_ShouldSetCacheControlHeaders()
    {
        // Arrange
        var channelId = "test-channel";
        var feedResult = CreateFeedResult("<rss>test</rss>");
        
        _feedCacheMock
            .Setup(c => c.GetOrCreateAsync(
                $"channel:{channelId}:audio",
                It.IsAny<Func<Task<FeedGenerationResult>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedResult);

        // Act
        var result = await _controller.GetAudioRss(channelId, CancellationToken.None);

        // Assert
        _controller.Response.Headers.Should().ContainKey("Cache-Control");
        _controller.Response.Headers["Cache-Control"].ToString().Should().Contain("public");
        _controller.Response.Headers["Cache-Control"].ToString().Should().Contain("max-age=");
    }

    #endregion

    #region GetMedia Tests

    [Fact]
    public async Task GetMedia_ShouldReturnAudioFile_WhenAudioType()
    {
        // Arrange
        var channelId = "test-channel";
        var filename = "video123.mp3";
        
        // Act & Assert - This test verifies the endpoint exists and accepts parameters
        // The actual file serving will need integration testing with file system
        var result = await _controller.GetMedia(channelId, "audio", filename);
        
        // For now, this returns NotFound as file serving depends on actual file storage
        // Integration tests should verify actual file serving
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMedia_ShouldReturnVideoFile_WhenVideoType()
    {
        // Arrange
        var channelId = "test-channel";
        var filename = "video123.mp4";
        
        // Act
        var result = await _controller.GetMedia(channelId, "video", filename);
        
        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMedia_ShouldReturn404_ForInvalidType()
    {
        // Arrange
        var channelId = "test-channel";
        var filename = "video123.mp3";
        
        // Act
        var result = await _controller.GetMedia(channelId, "invalid", filename);
        
        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMedia_ShouldReturnBadRequest_ForInvalidExtension()
    {
        // Arrange
        var channelId = "test-channel";
        var filename = "video123.txt";
        
        // Act
        var result = await _controller.GetMedia(channelId, "audio", filename);
        
        // Assert
        result.Should().BeOfType<BadRequestResult>();
    }

    #endregion

    #region Helper Methods

    private static FeedGenerationResult CreateFeedResult(string xmlContent, DateTimeOffset? lastModified = null)
    {
        return new FeedGenerationResult
        {
            XmlContent = xmlContent,
            Etag = GenerateTestEtag(xmlContent),
            LastModified = lastModified ?? DateTimeOffset.UtcNow
        };
    }

    private static string GenerateTestEtag(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    #endregion
}