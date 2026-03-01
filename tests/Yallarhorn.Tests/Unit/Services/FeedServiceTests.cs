namespace Yallarhorn.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Yallarhorn.Configuration;
using Yallarhorn.Data;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Models;
using Yallarhorn.Services;

public class FeedServiceTests : IDisposable
{
    private readonly YallarhornDbContext _context;
    private readonly EpisodeRepository _episodeRepository;
    private readonly ChannelRepository _channelRepository;
    private readonly Mock<IRssFeedBuilder> _rssFeedBuilderMock;
    private readonly Mock<IAtomFeedBuilder> _atomFeedBuilderMock;
    private readonly FeedService _service;

    private readonly Channel _testChannel;

    public FeedServiceTests()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"yallarhorn_feed_test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        _context = new YallarhornDbContext(options);
        _context.Database.EnsureCreated();
        _episodeRepository = new EpisodeRepository(_context);
        _channelRepository = new ChannelRepository(_context);
        _rssFeedBuilderMock = new Mock<IRssFeedBuilder>();
        _atomFeedBuilderMock = new Mock<IAtomFeedBuilder>();

        var serverOptions = Options.Create(new ServerOptions
        {
            BaseUrl = "http://localhost:8080"
        });

        _service = new FeedService(
            _channelRepository,
            _episodeRepository,
            _rssFeedBuilderMock.Object,
            _atomFeedBuilderMock.Object,
            serverOptions);

        _testChannel = new Channel
        {
            Id = "feed-test-channel",
            Url = "https://www.youtube.com/@feedtest",
            Title = "Feed Test Channel",
            Description = "A test channel for feed generation",
            EpisodeCountConfig = 50,
            FeedType = FeedType.Audio,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Channels.Add(_testChannel);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GenerateFeedAsync Tests

    [Fact]
    public async Task GenerateFeedAsync_ShouldReturnNull_WhenChannelNotFound()
    {
        var result = await _service.GenerateFeedAsync("nonexistent-channel", FeedType.Audio);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateFeedAsync_ShouldReturnFeedWithXmlContent()
    {
        // Arrange
        SetupCompletedEpisode("vid1", "Episode 1", FilePathAudio: "audio/ep1.mp3", FileSizeAudio: 1000000);
        _rssFeedBuilderMock
            .Setup(r => r.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<rss>test</rss>");

        // Act
        var result = await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Audio);

        // Assert
        result.Should().NotBeNull();
        result!.XmlContent.Should().Be("<rss>test</rss>");
    }

    [Fact]
    public async Task GenerateFeedAsync_ShouldReturnEtagHash()
    {
        // Arrange
        SetupCompletedEpisode("vid1", "Episode 1", FilePathAudio: "audio/ep1.mp3", FileSizeAudio: 1000000);
        _rssFeedBuilderMock
            .Setup(r => r.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<rss>test</rss>");

        // Act
        var result = await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Audio);

        // Assert
        result.Should().NotBeNull();
        result!.Etag.Should().NotBeNullOrEmpty();
        // Etag should be a SHA256 hash (64 chars for hex)
        result.Etag.Length.Should().Be(64);
    }

    [Fact]
    public async Task GenerateFeedAsync_ShouldReturnLastModified()
    {
        // Arrange
        var episode = SetupCompletedEpisode("vid1", "Episode 1", FilePathAudio: "audio/ep1.mp3", FileSizeAudio: 1000000);
        var beforeGenerate = DateTimeOffset.UtcNow;

        _rssFeedBuilderMock
            .Setup(r => r.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<rss>test</rss>");

        // Act
        var result = await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Audio);

        // Assert
        result.Should().NotBeNull();
        result!.LastModified.Should().BeOnOrAfter(beforeGenerate.AddSeconds(-1));
    }

    [Fact]
    public async Task GenerateFeedAsync_ShouldUseChannelEpisodeCountConfig()
    {
        // Arrange
        _testChannel.EpisodeCountConfig = 5;
        _context.SaveChanges();

        // Create 10 completed episodes
        for (int i = 1; i <= 10; i++)
        {
            SetupCompletedEpisode(
                $"vid{i}",
                $"Episode {i}",
                FilePathAudio: $"audio/ep{i}.mp3",
                FileSizeAudio: 1000000 * i,
                publishedAt: DateTimeOffset.UtcNow.AddDays(-i));
        }

        IEnumerable<Episode>? capturedEpisodes = null;
        _rssFeedBuilderMock
            .Setup(r => r.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<Channel, IEnumerable<Episode>, FeedType, string, string>((c, eps, ft, b, f) =>
            {
                capturedEpisodes = eps.ToList();
            })
            .Returns("<rss>test</rss>");

        // Act
        await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Audio);

        // Assert
        capturedEpisodes.Should().NotBeNull();
        capturedEpisodes.Should().HaveCount(5);
    }

    [Fact]
    public async Task GenerateFeedAsync_ShouldDefaultTo50Episodes_WhenConfigIsZero()
    {
        // Arrange
        _testChannel.EpisodeCountConfig = 0;
        _context.SaveChanges();

        // Create 60 episodes
        for (int i = 1; i <= 60; i++)
        {
            SetupCompletedEpisode(
                $"vid{i}",
                $"Episode {i}",
                FilePathAudio: $"audio/ep{i}.mp3",
                FileSizeAudio: 1000000 * i,
                publishedAt: DateTimeOffset.UtcNow.AddDays(-i));
        }

        IEnumerable<Episode>? capturedEpisodes = null;
        _rssFeedBuilderMock
            .Setup(r => r.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<Channel, IEnumerable<Episode>, FeedType, string, string>((c, eps, ft, b, f) =>
            {
                capturedEpisodes = eps.ToList();
            })
            .Returns("<rss>test</rss>");

        // Act
        await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Audio);

        // Assert
        capturedEpisodes.Should().NotBeNull();
        capturedEpisodes.Should().HaveCount(50);
    }

    [Fact]
    public async Task GenerateFeedAsync_ShouldFilterByAudioForAudioFeedType()
    {
        // Arrange
        var audioOnlyEpisode = SetupCompletedEpisode(
            "vid1", "Audio Only",
            FilePathAudio: "audio/ep1.mp3", FileSizeAudio: 1000000);
        var videoOnlyEpisode = SetupCompletedEpisode(
            "vid2", "Video Only",
            FilePathVideo: "video/ep2.mp4", FileSizeVideo: 2000000);
        var bothEpisode = SetupCompletedEpisode(
            "vid3", "Both",
            FilePathAudio: "audio/ep3.mp3", FileSizeAudio: 1500000,
            FilePathVideo: "video/ep3.mp4", FileSizeVideo: 3000000);

        IEnumerable<Episode>? capturedEpisodes = null;
        _rssFeedBuilderMock
            .Setup(r => r.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<Channel, IEnumerable<Episode>, FeedType, string, string>((c, eps, ft, b, f) =>
            {
                capturedEpisodes = eps.ToList();
            })
            .Returns("<rss>test</rss>");

        // Act
        await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Audio);

        // Assert
        capturedEpisodes.Should().NotBeNull();
        capturedEpisodes!.Select(e => e.VideoId).Should().Contain(new[] { "vid1", "vid3" });
        capturedEpisodes!.Select(e => e.VideoId).Should().NotContain("vid2");
    }

    [Fact]
    public async Task GenerateFeedAsync_ShouldFilterByVideoForVideoFeedType()
    {
        // Arrange
        var audioOnlyEpisode = SetupCompletedEpisode(
            "vid1", "Audio Only",
            FilePathAudio: "audio/ep1.mp3", FileSizeAudio: 1000000);
        var videoOnlyEpisode = SetupCompletedEpisode(
            "vid2", "Video Only",
            FilePathVideo: "video/ep2.mp4", FileSizeVideo: 2000000);
        var bothEpisode = SetupCompletedEpisode(
            "vid3", "Both",
            FilePathAudio: "audio/ep3.mp3", FileSizeAudio: 1500000,
            FilePathVideo: "video/ep3.mp4", FileSizeVideo: 3000000);

        IEnumerable<Episode>? capturedEpisodes = null;
        _rssFeedBuilderMock
            .Setup(r => r.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<Channel, IEnumerable<Episode>, FeedType, string, string>((c, eps, ft, b, f) =>
            {
                capturedEpisodes = eps.ToList();
            })
            .Returns("<rss>test</rss>");

        // Act
        await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Video);

        // Assert
        capturedEpisodes.Should().NotBeNull();
        capturedEpisodes!.Select(e => e.VideoId).Should().Contain(new[] { "vid2", "vid3" });
        capturedEpisodes!.Select(e => e.VideoId).Should().NotContain("vid1");
    }

    [Fact]
    public async Task GenerateFeedAsync_ShouldIncludeAllCompletedForBothFeedType()
    {
        // Arrange
        var audioOnlyEpisode = SetupCompletedEpisode(
            "vid1", "Audio Only",
            FilePathAudio: "audio/ep1.mp3", FileSizeAudio: 1000000);
        var videoOnlyEpisode = SetupCompletedEpisode(
            "vid2", "Video Only",
            FilePathVideo: "video/ep2.mp4", FileSizeVideo: 2000000);
        var bothEpisode = SetupCompletedEpisode(
            "vid3", "Both",
            FilePathAudio: "audio/ep3.mp3", FileSizeAudio: 1500000,
            FilePathVideo: "video/ep3.mp4", FileSizeVideo: 3000000);

        IEnumerable<Episode>? capturedEpisodes = null;
        _rssFeedBuilderMock
            .Setup(r => r.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<Channel, IEnumerable<Episode>, FeedType, string, string>((c, eps, ft, b, f) =>
            {
                capturedEpisodes = eps.ToList();
            })
            .Returns("<rss>test</rss>");

        // Act
        await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Both);

        // Assert
        capturedEpisodes.Should().NotBeNull();
        capturedEpisodes!.Select(e => e.VideoId).Should().Contain(new[] { "vid1", "vid2", "vid3" });
    }

    [Fact]
    public async Task GenerateFeedAsync_ShouldFilterCompletedEpisodesOnly()
    {
        // Arrange
        var completedEpisode = SetupCompletedEpisode(
            "vid1", "Completed",
            FilePathAudio: "audio/ep1.mp3", FileSizeAudio: 1000000);
        var pendingEpisode = CreateEpisode(
            "vid2", "Pending",
            status: EpisodeStatus.Pending);

        IEnumerable<Episode>? capturedEpisodes = null;
        _rssFeedBuilderMock
            .Setup(r => r.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<Channel, IEnumerable<Episode>, FeedType, string, string>((c, eps, ft, b, f) =>
            {
                capturedEpisodes = eps.ToList();
            })
            .Returns("<rss>test</rss>");

        // Act
        await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Audio);

        // Assert
        capturedEpisodes.Should().NotBeNull();
        capturedEpisodes.Should().ContainSingle();
        capturedEpisodes!.First().VideoId.Should().Be("vid1");
    }

    [Fact]
    public async Task GenerateFeedAsync_ShouldOrderByPublishedAtDescending()
    {
        // Arrange
        var olderEpisode = SetupCompletedEpisode(
            "vid1", "Older Episode",
            FilePathAudio: "audio/ep1.mp3", FileSizeAudio: 1000000,
            publishedAt: DateTimeOffset.UtcNow.AddDays(-10));
        var newerEpisode = SetupCompletedEpisode(
            "vid2", "Newer Episode",
            FilePathAudio: "audio/ep2.mp3", FileSizeAudio: 1000000,
            publishedAt: DateTimeOffset.UtcNow.AddDays(-1));
        var oldestEpisode = SetupCompletedEpisode(
            "vid3", "Oldest Episode",
            FilePathAudio: "audio/ep3.mp3", FileSizeAudio: 1000000,
            publishedAt: DateTimeOffset.UtcNow.AddDays(-30));

        IEnumerable<Episode>? capturedEpisodes = null;
        _rssFeedBuilderMock
            .Setup(r => r.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<Channel, IEnumerable<Episode>, FeedType, string, string>((c, eps, ft, b, f) =>
            {
                capturedEpisodes = eps.ToList();
            })
            .Returns("<rss>test</rss>");

        // Act
        await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Audio);

        // Assert
        capturedEpisodes.Should().NotBeNull();
        var episodeList = capturedEpisodes!.ToList();
        episodeList[0].VideoId.Should().Be("vid2"); // Newest (1 day ago)
        episodeList[1].VideoId.Should().Be("vid1"); // 10 days ago
        episodeList[2].VideoId.Should().Be("vid3"); // Oldest (30 days ago)
    }

    [Fact]
    public async Task GenerateFeedAsync_ShouldHandleEpisodesWithNullPublishedAt()
    {
        // Arrange
        var episodeWithDate = SetupCompletedEpisode(
            "vid1", "Episode With Date",
            FilePathAudio: "audio/ep1.mp3", FileSizeAudio: 1000000,
            publishedAt: DateTimeOffset.UtcNow.AddDays(-1));
        var episodeWithoutDate = SetupCompletedEpisode(
            "vid2", "Episode Without Date",
            FilePathAudio: "audio/ep2.mp3", FileSizeAudio: 1000000,
            publishedAt: null);

        IEnumerable<Episode>? capturedEpisodes = null;
        _rssFeedBuilderMock
            .Setup(r => r.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<Channel, IEnumerable<Episode>, FeedType, string, string>((c, eps, ft, b, f) =>
            {
                capturedEpisodes = eps.ToList();
            })
            .Returns("<rss>test</rss>");

        // Act
        await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Audio);

        // Assert - Should not throw, both episodes included
        capturedEpisodes.Should().NotBeNull();
        capturedEpisodes.Should().HaveCount(2);
    }

    [Fact]
    public async Task GenerateFeedAsync_ShouldProduceSameEtagForSameContent()
    {
        // Arrange
        SetupCompletedEpisode("vid1", "Episode 1", FilePathAudio: "audio/ep1.mp3", FileSizeAudio: 1000000);
        _rssFeedBuilderMock
            .Setup(r => r.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<rss>same-content</rss>");

        // Act - Generate twice
        var result1 = await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Audio);
        var result2 = await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Audio);

        // Assert
        result1!.Etag.Should().Be(result2!.Etag);
    }

    [Fact]
    public async Task GenerateFeedAsync_ShouldProduceDifferentEtagForDifferentContent()
    {
        // Arrange
        SetupCompletedEpisode("vid1", "Episode 1", FilePathAudio: "audio/ep1.mp3", FileSizeAudio: 1000000);

        int callCount = 0;
        _rssFeedBuilderMock
            .Setup(r => r.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(() => callCount++ == 0 ? "<rss>content-1</rss>" : "<rss>content-2</rss>");

        // Act
        var result1 = await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Audio);
        var result2 = await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Audio);

        // Assert
        result1!.Etag.Should().NotBe(result2!.Etag);
    }

    [Fact]
    public async Task GenerateFeedAsync_ShouldUseDefaultEpisodeCount_WhenConfigIsNegative()
    {
        // Arrange
        _testChannel.EpisodeCountConfig = -5;
        _context.SaveChanges();

        for (int i = 1; i <= 60; i++)
        {
            SetupCompletedEpisode(
                $"vid{i}",
                $"Episode {i}",
                FilePathAudio: $"audio/ep{i}.mp3",
                FileSizeAudio: 1000000 * i,
                publishedAt: DateTimeOffset.UtcNow.AddDays(-i));
        }

        IEnumerable<Episode>? capturedEpisodes = null;
        _rssFeedBuilderMock
            .Setup(r => r.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<Channel, IEnumerable<Episode>, FeedType, string, string>((c, eps, ft, b, f) =>
            {
                capturedEpisodes = eps.ToList();
            })
            .Returns("<rss>test</rss>");

        // Act
        await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Audio);

        // Assert
        capturedEpisodes.Should().NotBeNull();
        capturedEpisodes.Should().HaveCount(50);
    }

    [Fact]
    public async Task GenerateFeedAsync_ShouldReturnEmptyFeed_WhenNoCompletedEpisodes()
    {
        // Arrange - No completed episodes
        _rssFeedBuilderMock
            .Setup(r => r.BuildRssFeed(
                It.IsAny<Channel>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<FeedType>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns("<rss><channel></channel></rss>");

        // Act
        var result = await _service.GenerateFeedAsync(_testChannel.Id, FeedType.Audio);

        // Assert
        result.Should().NotBeNull();
        result!.XmlContent.Should().Contain("<channel>");
    }

    #endregion

    #region Helper Methods

    private Episode SetupCompletedEpisode(
        string videoId,
        string title,
        string? FilePathAudio = null,
        long? FileSizeAudio = null,
        string? FilePathVideo = null,
        long? FileSizeVideo = null,
        DateTimeOffset? publishedAt = null)
    {
        var episode = new Episode
        {
            Id = $"ep-{videoId}",
            VideoId = videoId,
            ChannelId = _testChannel.Id,
            Title = title,
            Description = $"Description for {title}",
            FilePathAudio = FilePathAudio,
            FileSizeAudio = FileSizeAudio,
            FilePathVideo = FilePathVideo,
            FileSizeVideo = FileSizeVideo,
            DurationSeconds = 300,
            PublishedAt = publishedAt ?? DateTimeOffset.UtcNow.AddDays(-1),
            Status = EpisodeStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.Add(episode);
        _context.SaveChanges();
        return episode;
    }

    private Episode CreateEpisode(
        string videoId,
        string title,
        EpisodeStatus status)
    {
        var episode = new Episode
        {
            Id = $"ep-{videoId}",
            VideoId = videoId,
            ChannelId = _testChannel.Id,
            Title = title,
            Description = $"Description for {title}",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.Add(episode);
        _context.SaveChanges();
        return episode;
    }

    #endregion
}