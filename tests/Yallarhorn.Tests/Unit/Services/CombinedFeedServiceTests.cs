namespace Yallarhorn.Tests.Unit.Services;

using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Yallarhorn.Configuration;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Models;
using Yallarhorn.Services;

public class CombinedFeedServiceTests
{
    private const string BaseUrl = "http://localhost:8080";
    private const string FeedPath = "/feeds";
    private readonly Mock<IChannelRepository> _channelRepositoryMock;
    private readonly Mock<IEpisodeRepository> _episodeRepositoryMock;
    private readonly Mock<IRssFeedBuilder> _rssFeedBuilderMock;
    private readonly ICombinedFeedService _service;

    public CombinedFeedServiceTests()
    {
        _channelRepositoryMock = new Mock<IChannelRepository>();
        _episodeRepositoryMock = new Mock<IEpisodeRepository>();
        _rssFeedBuilderMock = new Mock<IRssFeedBuilder>();

        var serverOptions = Options.Create(new ServerOptions
        {
            BaseUrl = BaseUrl
        });

        _service = new CombinedFeedService(
            _channelRepositoryMock.Object,
            _episodeRepositoryMock.Object,
            _rssFeedBuilderMock.Object,
            serverOptions);
    }

    #region Interface Tests

    [Fact]
    public void CombinedFeedService_ShouldImplementICombinedFeedService()
    {
        // Assert
        _service.Should().BeAssignableTo<ICombinedFeedService>();
    }

    #endregion

    #region GenerateCombinedFeedAsync Tests

    [Fact]
    public async Task GenerateCombinedFeedAsync_WithNoEnabledChannels_ShouldReturnEmptyFeed()
    {
        // Arrange
        _channelRepositoryMock
            .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _rssFeedBuilderMock
            .Setup(b => b.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<rss version=\"2.0\"><channel></channel></rss>");

        // Act
        var result = await _service.GenerateCombinedFeedAsync(FeedType.Audio);

        // Assert
        result.Should().NotBeNull();
        result.XmlContent.Should().NotBeNullOrEmpty();
        result.Etag.Should().NotBeNullOrEmpty();
        result.LastModified.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        _channelRepositoryMock.Verify(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()), Times.Once);
        _rssFeedBuilderMock.Verify(b => b.BuildRssFeed(
            It.IsAny<Channel>(),
            It.Is<IEnumerable<Episode>>(e => !e.Any()),
            FeedType.Audio,
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GenerateCombinedFeedAsync_WithEnabledChannels_ShouldAggregateAllDownloadedEpisodes()
    {
        // Arrange
        var channel1 = CreateTestChannel(id: "ch1", title: "Tech Talk");
        var channel2 = CreateTestChannel(id: "ch2", title: "Science Hour");
        var channel3 = CreateTestChannel(id: "ch3", title: "Music Vibes", enabled: false);

        _channelRepositoryMock
            .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([channel1, channel2]);

        _episodeRepositoryMock
            .Setup(r => r.GetDownloadedAsync(channel1.Id, FeedType.Audio, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateTestEpisode(channel1, "vid1", "Episode 1")]);

        _episodeRepositoryMock
            .Setup(r => r.GetDownloadedAsync(channel2.Id, FeedType.Audio, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateTestEpisode(channel2, "vid2", "Episode 2")]);

        _rssFeedBuilderMock
            .Setup(b => b.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<rss version=\"2.0\"><channel></channel></rss>");

        // Act
        var result = await _service.GenerateCombinedFeedAsync(FeedType.Audio);

        // Assert
        result.Should().NotBeNull();

        _episodeRepositoryMock.Verify(r => r.GetDownloadedAsync(channel1.Id, FeedType.Audio, 100, It.IsAny<CancellationToken>()), Times.Once);
        _episodeRepositoryMock.Verify(r => r.GetDownloadedAsync(channel2.Id, FeedType.Audio, 100, It.IsAny<CancellationToken>()), Times.Once);
        // Verify disabled channel 3 was never queried
        _episodeRepositoryMock.Verify(r => r.GetDownloadedAsync(channel3.Id, It.IsAny<FeedType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateCombinedFeedAsync_WithMoreThan100Episodes_ShouldLimitTo100()
    {
        // Arrange
        var channel1 = CreateTestChannel(id: "ch1", title: "Channel 1");
        var channel2 = CreateTestChannel(id: "ch2", title: "Channel 2");

        _channelRepositoryMock
            .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([channel1, channel2]);

        // Create 60 episodes per channel (more than 100 total)
        var episodes1 = Enumerable.Range(1, 60)
            .Select(i => CreateTestEpisode(channel1, $"vid1-{i}", $"Episode 1-{i}", publishedAt: DateTimeOffset.UtcNow.AddDays(-i)))
            .ToList();

        var episodes2 = Enumerable.Range(1, 60)
            .Select(i => CreateTestEpisode(channel2, $"vid2-{i}", $"Episode 2-{i}", publishedAt: DateTimeOffset.UtcNow.AddDays(-i).AddHours(-12)))
            .ToList();

        _episodeRepositoryMock
            .Setup(r => r.GetDownloadedAsync(channel1.Id, FeedType.Audio, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episodes1);

        _episodeRepositoryMock
            .Setup(r => r.GetDownloadedAsync(channel2.Id, FeedType.Audio, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episodes2);

        List<Episode>? capturedEpisodes = null;
        _rssFeedBuilderMock
            .Setup(b => b.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<Channel, IEnumerable<Episode>, FeedType, string, string>((_, episodes, _, _, _) =>
            {
                capturedEpisodes = episodes.ToList();
            })
            .Returns("<rss version=\"2.0\"><channel></channel></rss>");

        // Act
        var result = await _service.GenerateCombinedFeedAsync(FeedType.Audio);

        // Assert
        capturedEpisodes.Should().NotBeNull();
        capturedEpisodes!.Count.Should().Be(100);
    }

    [Fact]
    public async Task GenerateCombinedFeedAsync_ShouldOrderEpisodesByPublishedAtDescending()
    {
        // Arrange
        var channel1 = CreateTestChannel(id: "ch1", title: "Channel 1");
        var channel2 = CreateTestChannel(id: "ch2", title: "Channel 2");

        _channelRepositoryMock
            .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([channel1, channel2]);

        var episode1 = CreateTestEpisode(channel1, "vid1", "Old Episode", publishedAt: DateTimeOffset.Parse("2024-01-10"));
        var episode2 = CreateTestEpisode(channel2, "vid2", "New Episode", publishedAt: DateTimeOffset.Parse("2024-01-20"));
        var episode3 = CreateTestEpisode(channel1, "vid3", "Middle Episode", publishedAt: DateTimeOffset.Parse("2024-01-15"));

        _episodeRepositoryMock
            .Setup(r => r.GetDownloadedAsync(channel1.Id, FeedType.Audio, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([episode1, episode3]);

        _episodeRepositoryMock
            .Setup(r => r.GetDownloadedAsync(channel2.Id, FeedType.Audio, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([episode2]);

        List<Episode>? capturedEpisodes = null;
        _rssFeedBuilderMock
            .Setup(b => b.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<Channel, IEnumerable<Episode>, FeedType, string, string>((_, episodes, _, _, _) =>
            {
                capturedEpisodes = episodes.ToList();
            })
            .Returns("<rss version=\"2.0\"><channel></channel></rss>");

        // Act
        var result = await _service.GenerateCombinedFeedAsync(FeedType.Audio);

        // Assert
        capturedEpisodes.Should().NotBeNull();
        capturedEpisodes!.Count.Should().Be(3);
        capturedEpisodes[0].VideoId.Should().Be("vid2"); // Newest first
        capturedEpisodes[1].VideoId.Should().Be("vid3"); // Middle
        capturedEpisodes[2].VideoId.Should().Be("vid1"); // Oldest last
    }

    [Fact]
    public async Task GenerateCombinedFeedAsync_ShouldCreateSyntheticChannelWithAllChannelsTitle()
    {
        // Arrange
        _channelRepositoryMock
            .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Channel? capturedChannel = null;
        _rssFeedBuilderMock
            .Setup(b => b.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<Channel, IEnumerable<Episode>, FeedType, string, string>((channel, _, _, _, _) =>
            {
                capturedChannel = channel;
            })
            .Returns("<rss version=\"2.0\"><channel></channel></rss>");

        // Act
        var result = await _service.GenerateCombinedFeedAsync(FeedType.Audio);

        // Assert
        capturedChannel.Should().NotBeNull();
        capturedChannel!.Title.Should().Be("All Channels");
        capturedChannel.Description.Should().Be("Combined feed from all channels");
        capturedChannel.Id.Should().Be("combined");
    }

    [Fact]
    public async Task GenerateCombinedFeedAsync_ShouldPassBaseUrlAndFeedPathToBuilder()
    {
        // Arrange
        _channelRepositoryMock
            .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        string? capturedBaseUrl = null;
        string? capturedFeedPath = null;

        _rssFeedBuilderMock
            .Setup(b => b.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<Channel, IEnumerable<Episode>, FeedType, string, string>((_, _, _, baseUrl, feedPath) =>
            {
                capturedBaseUrl = baseUrl;
                capturedFeedPath = feedPath;
            })
            .Returns("<rss version=\"2.0\"><channel></channel></rss>");

        // Act
        var result = await _service.GenerateCombinedFeedAsync(FeedType.Video);

        // Assert
        capturedBaseUrl.Should().NotBeNull();
        capturedFeedPath.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateCombinedFeedAsync_AudioFeedType_ShouldRequestAudioEpisodes()
    {
        // Arrange
        var channel = CreateTestChannel(id: "ch1", title: "Test Channel");

        _channelRepositoryMock
            .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([channel]);

        _episodeRepositoryMock
            .Setup(r => r.GetDownloadedAsync(channel.Id, FeedType.Audio, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _rssFeedBuilderMock
            .Setup(b => b.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                FeedType.Audio,
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<rss version=\"2.0\"><channel></channel></rss>");

        // Act
        var result = await _service.GenerateCombinedFeedAsync(FeedType.Audio);

        // Assert
        _episodeRepositoryMock.Verify(r => r.GetDownloadedAsync(channel.Id, FeedType.Audio, 100, It.IsAny<CancellationToken>()), Times.Once);
        _rssFeedBuilderMock.Verify(b => b.BuildRssFeed(
            It.IsAny<Channel>(),
            It.IsAny<IEnumerable<Episode>>(),
            FeedType.Audio,
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GenerateCombinedFeedAsync_VideoFeedType_ShouldRequestVideoEpisodes()
    {
        // Arrange
        var channel = CreateTestChannel(id: "ch1", title: "Test Channel");

        _channelRepositoryMock
            .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([channel]);

        _episodeRepositoryMock
            .Setup(r => r.GetDownloadedAsync(channel.Id, FeedType.Video, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _rssFeedBuilderMock
            .Setup(b => b.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                FeedType.Video,
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<rss version=\"2.0\"><channel></channel></rss>");

        // Act
        var result = await _service.GenerateCombinedFeedAsync(FeedType.Video);

        // Assert
        _episodeRepositoryMock.Verify(r => r.GetDownloadedAsync(channel.Id, FeedType.Video, 100, It.IsAny<CancellationToken>()), Times.Once);
        _rssFeedBuilderMock.Verify(b => b.BuildRssFeed(
            It.IsAny<Channel>(),
            It.IsAny<IEnumerable<Episode>>(),
            FeedType.Video,
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GenerateCombinedFeedAsync_ShouldReturnEtagHash()
    {
        // Arrange
        var xmlContent = "<rss version=\"2.0\"><channel><title>All Channels</title></channel></rss>";

        _channelRepositoryMock
            .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _rssFeedBuilderMock
            .Setup(b => b.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(xmlContent);

        // Act
        var result = await _service.GenerateCombinedFeedAsync(FeedType.Audio);

        // Assert
        result.Etag.Should().NotBeNullOrEmpty();
        // ETag should be a hex string (SHA256 produces 64 hex characters)
        result.Etag.Should().MatchRegex("^[a-f0-9]{64}$");
    }

    [Fact]
    public async Task GenerateCombinedFeedAsync_ShouldReturnUniqueEtagsForDifferentContent()
    {
        // Arrange
        var channel = CreateTestChannel(id: "ch1", title: "Test Channel");

        _channelRepositoryMock
            .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([channel]);

        _episodeRepositoryMock
            .Setup(r => r.GetDownloadedAsync(channel.Id, FeedType.Audio, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateTestEpisode(channel, "vid1", "Episode 1")]);

        _rssFeedBuilderMock
            .Setup(b => b.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<rss><channel/></rss>");

        // Act
        var result1 = await _service.GenerateCombinedFeedAsync(FeedType.Audio);

        // Setup with different result
        _rssFeedBuilderMock
            .Setup(b => b.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<rss><channel><title>Different</title></channel></rss>");

        var result2 = await _service.GenerateCombinedFeedAsync(FeedType.Video);

        // Assert
        result1.Etag.Should().NotBe(result2.Etag);
    }

    [Fact]
    public async Task GenerateCombinedFeedAsync_ShouldSetLastModifiedToUtcNow()
    {
        // Arrange
        _channelRepositoryMock
            .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _rssFeedBuilderMock
            .Setup(b => b.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<rss version=\"2.0\"><channel></channel></rss>");

        // Act
        var before = DateTimeOffset.UtcNow.AddMilliseconds(-100); // Allow some tolerance
        var result = await _service.GenerateCombinedFeedAsync(FeedType.Audio);
        var after = DateTimeOffset.UtcNow.AddMilliseconds(100); // Allow some tolerance

        // Assert
        result.LastModified.Should().BeOnOrAfter(before);
        result.LastModified.Should().BeOnOrBefore(after);
    }

    [Fact]
    public async Task GenerateCombinedFeedAsync_EpisodesWithNullPublishedDate_ShouldBeOrderedLast()
    {
        // Arrange
        var channel = CreateTestChannel(id: "ch1", title: "Channel");

        _channelRepositoryMock
            .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([channel]);

        var episodeWithDate = CreateTestEpisode(channel, "vid1", "Has Date", publishedAt: DateTimeOffset.Parse("2024-01-15"));
        var episodeWithoutDate = CreateTestEpisode(channel, "vid2", "No Date", publishedAt: null);

        _episodeRepositoryMock
            .Setup(r => r.GetDownloadedAsync(channel.Id, FeedType.Audio, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([episodeWithDate, episodeWithoutDate]);

        List<Episode>? capturedEpisodes = null;
        _rssFeedBuilderMock
            .Setup(b => b.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<Channel, IEnumerable<Episode>, FeedType, string, string>((_, episodes, _, _, _) =>
            {
                capturedEpisodes = episodes.ToList();
            })
            .Returns("<rss version=\"2.0\"><channel></channel></rss>");

        // Act
        var result = await _service.GenerateCombinedFeedAsync(FeedType.Audio);

        // Assert
        capturedEpisodes.Should().NotBeNull();
        capturedEpisodes!.Count.Should().Be(2);
        capturedEpisodes[0].VideoId.Should().Be("vid1"); // Has date comes first
        capturedEpisodes[1].VideoId.Should().Be("vid2"); // Null date comes last
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task GenerateCombinedFeedAsync_ShouldPassCancellationTokenToRepository()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _channelRepositoryMock
            .Setup(r => r.GetEnabledAsync(token))
            .ReturnsAsync([]);

        _rssFeedBuilderMock
            .Setup(b => b.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<rss version=\"2.0\"><channel></channel></rss>");

        // Act
        await _service.GenerateCombinedFeedAsync(FeedType.Audio, token);

        // Assert
        _channelRepositoryMock.Verify(r => r.GetEnabledAsync(token), Times.Once);
    }

    [Fact]
    public async Task GenerateCombinedFeedAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _channelRepositoryMock
            .Setup(r => r.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .Throws(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _service.GenerateCombinedFeedAsync(FeedType.Audio, cts.Token));
    }

    #endregion

    #region Helper Methods

    private static Channel CreateTestChannel(string id, string title, bool enabled = true)
    {
        return new Channel
        {
            Id = id,
            Title = title,
            Url = $"https://youtube.com/@{title.ToLower().Replace(" ", "")}",
            Description = $"Description for {title}",
            Enabled = enabled,
            FeedType = FeedType.Audio,
            EpisodeCountConfig = 50,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static Episode CreateTestEpisode(Channel channel, string videoId, string title, DateTimeOffset? publishedAt = null)
    {
        return new Episode
        {
            Id = Guid.NewGuid().ToString("N"),
            VideoId = videoId,
            ChannelId = channel.Id,
            Channel = channel,
            Title = title,
            Description = $"Description for {title}",
            PublishedAt = publishedAt,
            FilePathAudio = $"test/audio/{videoId}.mp3",
            FileSizeAudio = 1024,
            Status = EpisodeStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    #endregion
}