namespace Yallarhorn.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Yallarhorn.Data;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Models;
using Yallarhorn.Services;

public class ChannelRefreshServiceTests : IDisposable
{
    private readonly YallarhornDbContext _context;
    private readonly EpisodeRepository _episodeRepository;
    private readonly ChannelRepository _channelRepository;
    private readonly DownloadQueueRepository _queueRepository;
    private readonly Mock<IYtDlpClient> _ytDlpClientMock;
    private readonly Mock<IDownloadQueueService> _queueServiceMock;
    private readonly ChannelRefreshService _service;

    private readonly Channel _testChannel;

    public ChannelRefreshServiceTests()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"yallarhorn_refresh_test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        _context = new YallarhornDbContext(options);
        _context.Database.EnsureCreated();
        _episodeRepository = new EpisodeRepository(_context);
        _channelRepository = new ChannelRepository(_context);
        _queueRepository = new DownloadQueueRepository(_context);
        _ytDlpClientMock = new Mock<IYtDlpClient>();
        _queueServiceMock = new Mock<IDownloadQueueService>();

        _service = new ChannelRefreshService(
            _channelRepository,
            _episodeRepository,
            _ytDlpClientMock.Object,
            _queueServiceMock.Object);

        _testChannel = new Channel
        {
            Id = "refresh-test-channel",
            Url = "https://www.youtube.com/@refreshtest",
            Title = "Refresh Test Channel",
            EpisodeCountConfig = 10,
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

    #region RefreshChannelAsync Tests

    [Fact]
    public async Task RefreshChannelAsync_ShouldReturnEmptyResult_WhenChannelNotFound()
    {
        var result = await _service.RefreshChannelAsync("nonexistent-channel");

        result.ChannelId.Should().Be("nonexistent-channel");
        result.VideosFound.Should().Be(0);
        result.EpisodesQueued.Should().Be(0);
        result.RefreshedAt.Should().BeNull();
    }

    [Fact]
    public async Task RefreshChannelAsync_ShouldFetchVideosFromYtDlp()
    {
        var videos = new List<YtDlpMetadata>
        {
            CreateVideoMetadata("vid1", "Video 1"),
            CreateVideoMetadata("vid2", "Video 2")
        };

        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(_testChannel.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(videos);

        _queueServiceMock
            .Setup(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string epId, int pri, CancellationToken ct) => new DownloadQueue
            {
                Id = $"queue-{epId}",
                EpisodeId = epId,
                Priority = pri,
                Status = QueueStatus.Pending
            });

        await _service.RefreshChannelAsync(_testChannel.Id);

        _ytDlpClientMock.Verify(
            y => y.GetChannelVideosAsync(_testChannel.Url, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshChannelAsync_ShouldCreateEpisodesForNewVideos()
    {
        var videos = new List<YtDlpMetadata>
        {
            CreateVideoMetadata("vid1", "Video 1", DateTimeOffset.UtcNow.AddDays(-1), 600, "Desc 1", "thumb1.jpg"),
            CreateVideoMetadata("vid2", "Video 2", DateTimeOffset.UtcNow.AddDays(-2), 1200, "Desc 2", "thumb2.jpg")
        };

        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(_testChannel.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(videos);

        _queueServiceMock
            .Setup(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string epId, int pri, CancellationToken ct) => new DownloadQueue
            {
                Id = $"queue-{epId}",
                EpisodeId = epId,
                Priority = pri,
                Status = QueueStatus.Pending
            });

        var result = await _service.RefreshChannelAsync(_testChannel.Id);

        result.VideosFound.Should().Be(2);
        result.EpisodesQueued.Should().Be(2);

        // Verify episodes were created in the database
        var episode1 = await _episodeRepository.GetByVideoIdAsync("vid1");
        episode1.Should().NotBeNull();
        episode1!.Title.Should().Be("Video 1");
        episode1.Description.Should().Be("Desc 1");
        episode1.DurationSeconds.Should().Be(600);
        episode1.ThumbnailUrl.Should().Be("thumb1.jpg");
        episode1.Status.Should().Be(EpisodeStatus.Pending);

        var episode2 = await _episodeRepository.GetByVideoIdAsync("vid2");
        episode2.Should().NotBeNull();
        episode2!.Title.Should().Be("Video 2");
    }

    [Fact]
    public async Task RefreshChannelAsync_ShouldNotCreateDuplicateEpisodes()
    {
        // Pre-create an existing episode
        var existingEpisode = new Episode
        {
            Id = "existing-ep-1",
            VideoId = "vid1",
            ChannelId = _testChannel.Id,
            Title = "Existing Episode 1",
            Status = EpisodeStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.Add(existingEpisode);
        _context.SaveChanges();

        var videos = new List<YtDlpMetadata>
        {
            CreateVideoMetadata("vid1", "Video 1 Updated"), // Already exists
            CreateVideoMetadata("vid2", "Video 2") // New
        };

        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(_testChannel.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(videos);

        _queueServiceMock
            .Setup(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string epId, int pri, CancellationToken ct) => new DownloadQueue
            {
                Id = $"queue-{epId}",
                EpisodeId = epId,
                Priority = pri,
                Status = QueueStatus.Pending
            });

        var result = await _service.RefreshChannelAsync(_testChannel.Id);

        result.VideosFound.Should().Be(2);
        result.EpisodesQueued.Should().Be(1); // Only vid2 is new

        // Verify only vid2 episode was created
        var allEpisodes = await _episodeRepository.GetByChannelIdAsync(_testChannel.Id);
        allEpisodes.Should().HaveCount(2); // existing + new
    }

    [Fact]
    public async Task RefreshChannelAsync_ShouldApplyRollingWindow()
    {
        // Channel only keeps 3 episodes
        _testChannel.EpisodeCountConfig = 3;
        _context.SaveChanges();

        var videos = new List<YtDlpMetadata>
        {
            CreateVideoMetadata("vid1", "Video 1", DateTimeOffset.UtcNow.AddDays(-1)),
            CreateVideoMetadata("vid2", "Video 2", DateTimeOffset.UtcNow.AddDays(-2)),
            CreateVideoMetadata("vid3", "Video 3", DateTimeOffset.UtcNow.AddDays(-3)),
            CreateVideoMetadata("vid4", "Video 4", DateTimeOffset.UtcNow.AddDays(-4)),
            CreateVideoMetadata("vid5", "Video 5", DateTimeOffset.UtcNow.AddDays(-5))
        };

        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(_testChannel.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(videos);

        _queueServiceMock
            .Setup(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string epId, int pri, CancellationToken ct) => new DownloadQueue
            {
                Id = $"queue-{epId}",
                EpisodeId = epId,
                Priority = pri,
                Status = QueueStatus.Pending
            });

        var result = await _service.RefreshChannelAsync(_testChannel.Id);

        result.VideosFound.Should().Be(5);
        result.EpisodesQueued.Should().Be(3); // Only top 3 by published_at

        // Verify the newest 3 were queued (vid1, vid2, vid3)
        _queueServiceMock.Verify(
            q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task RefreshChannelAsync_ShouldSortByPublishedAtDescending()
    {
        _testChannel.EpisodeCountConfig = 2;
        _context.SaveChanges();

        // Videos in random order
        var videos = new List<YtDlpMetadata>
        {
            CreateVideoMetadata("vid3", "Video 3", DateTimeOffset.UtcNow.AddDays(-3)),
            CreateVideoMetadata("vid1", "Video 1", DateTimeOffset.UtcNow.AddDays(-1)),
            CreateVideoMetadata("vid2", "Video 2", DateTimeOffset.UtcNow.AddDays(-2))
        };

        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(_testChannel.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(videos);

        var enqueuedVideoIds = new List<string>();
        _queueServiceMock
            .Setup(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, CancellationToken>((epId, pri, ct) =>
            {
                // Look up the episode to get its video_id
                var episode = _context.Episodes.Find(epId);
                if (episode != null)
                {
                    enqueuedVideoIds.Add(episode.VideoId);
                }
            })
            .ReturnsAsync((string epId, int pri, CancellationToken ct) => new DownloadQueue
            {
                Id = $"queue-{epId}",
                EpisodeId = epId,
                Priority = pri,
                Status = QueueStatus.Pending
            });

        await _service.RefreshChannelAsync(_testChannel.Id);

        // Should have vid1 and vid2 (newest two)
        enqueuedVideoIds.Should().Contain(new[] { "vid1", "vid2" });
        enqueuedVideoIds.Should().NotContain("vid3");
    }

    [Fact]
    public async Task RefreshChannelAsync_ShouldUpdateChannelLastRefreshAt()
    {
        var beforeRefresh = DateTimeOffset.UtcNow;

        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(_testChannel.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YtDlpMetadata>());

        await _service.RefreshChannelAsync(_testChannel.Id);

        // Detach and re-query to get fresh data
        _context.Entry(_testChannel).State = EntityState.Detached;
        var updatedChannel = await _channelRepository.GetByIdAsync(_testChannel.Id);

        updatedChannel!.LastRefreshAt.Should().NotBeNull();
        updatedChannel.LastRefreshAt.Should().BeOnOrAfter(beforeRefresh);
    }

    [Fact]
    public async Task RefreshChannelAsync_ShouldSetRefreshedAtInResult()
    {
        var beforeRefresh = DateTimeOffset.UtcNow;

        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(_testChannel.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YtDlpMetadata>());

        var result = await _service.RefreshChannelAsync(_testChannel.Id);

        result.RefreshedAt.Should().NotBeNull();
        result.RefreshedAt.Should().BeOnOrAfter(beforeRefresh);
    }

    [Fact]
    public async Task RefreshChannelAsync_ShouldQueueNewEpisodes()
    {
        var videos = new List<YtDlpMetadata>
        {
            CreateVideoMetadata("vid1", "Video 1"),
            CreateVideoMetadata("vid2", "Video 2")
        };

        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(_testChannel.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(videos);

        _queueServiceMock
            .Setup(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string epId, int pri, CancellationToken ct) => new DownloadQueue
            {
                Id = $"queue-{epId}",
                EpisodeId = epId,
                Priority = pri,
                Status = QueueStatus.Pending
            });

        await _service.RefreshChannelAsync(_testChannel.Id);

        _queueServiceMock.Verify(
            q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RefreshChannelAsync_ShouldHandleYtDlpErrors()
    {
        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(_testChannel.Url, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new YtDlpException("Failed to fetch channel"));

        var act = () => _service.RefreshChannelAsync(_testChannel.Id);

        await act.Should().ThrowAsync<YtDlpException>()
            .WithMessage("*Failed to fetch channel*");
    }

    [Fact]
    public async Task RefreshChannelAsync_ShouldHandleEmptyVideoList()
    {
        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(_testChannel.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YtDlpMetadata>());

        var result = await _service.RefreshChannelAsync(_testChannel.Id);

        result.VideosFound.Should().Be(0);
        result.EpisodesQueued.Should().Be(0);
    }

    [Fact]
    public async Task RefreshChannelAsync_ShouldHandleVideosWithNoTimestamp()
    {
        var videos = new List<YtDlpMetadata>
        {
            CreateVideoMetadata("vid1", "Video 1", null), // No timestamp
            CreateVideoMetadata("vid2", "Video 2", DateTimeOffset.UtcNow.AddDays(-1))
        };

        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(_testChannel.Url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(videos);

        _queueServiceMock
            .Setup(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string epId, int pri, CancellationToken ct) => new DownloadQueue
            {
                Id = $"queue-{epId}",
                EpisodeId = epId,
                Priority = pri,
                Status = QueueStatus.Pending
            });

        var result = await _service.RefreshChannelAsync(_testChannel.Id);

        result.VideosFound.Should().Be(2);
        result.EpisodesQueued.Should().Be(2);

        // Verify episodes were created with null PublishedAt
        var episode1 = await _episodeRepository.GetByVideoIdAsync("vid1");
        episode1!.PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task RefreshChannelAsync_ShouldRespectCancellationToken()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(_testChannel.Url, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _service.RefreshChannelAsync(_testChannel.Id, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region RefreshAllChannelsAsync Tests

    [Fact]
    public async Task RefreshAllChannelsAsync_ShouldRefreshAllEnabledChannels()
    {
        // Add another enabled channel
        var channel2 = new Channel
        {
            Id = "refresh-test-channel-2",
            Url = "https://www.youtube.com/@refreshtest2",
            Title = "Refresh Test Channel 2",
            EpisodeCountConfig = 5,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Channels.Add(channel2);
        _context.SaveChanges();

        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YtDlpMetadata>
            {
                CreateVideoMetadata("vid1", "Video 1")
            });

        var results = (await _service.RefreshAllChannelsAsync()).ToList();

        results.Should().HaveCount(2);
        results.Should().Contain(r => r.ChannelId == _testChannel.Id);
        results.Should().Contain(r => r.ChannelId == channel2.Id);
    }

    [Fact]
    public async Task RefreshAllChannelsAsync_ShouldSkipDisabledChannels()
    {
        // Add a disabled channel
        var disabledChannel = new Channel
        {
            Id = "disabled-channel",
            Url = "https://www.youtube.com/@disabled",
            Title = "Disabled Channel",
            EpisodeCountConfig = 5,
            Enabled = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Channels.Add(disabledChannel);
        _context.SaveChanges();

        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<YtDlpMetadata>());

        var results = await _service.RefreshAllChannelsAsync();

        results.Should().ContainSingle(r => r.ChannelId == _testChannel.Id);
        results.Should().NotContain(r => r.ChannelId == disabledChannel.Id);
    }

    [Fact]
    public async Task RefreshAllChannelsAsync_ShouldContinueOnError()
    {
        // Add another enabled channel
        var channel2 = new Channel
        {
            Id = "refresh-test-channel-2",
            Url = "https://www.youtube.com/@refreshtest2",
            Title = "Refresh Test Channel 2",
            EpisodeCountConfig = 5,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Channels.Add(channel2);
        _context.SaveChanges();

        var callOrder = new List<string>();
        
        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(_testChannel.Url, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("channel1-called"))
            .ThrowsAsync(new YtDlpException("Error on channel 1"));

        _ytDlpClientMock
            .Setup(y => y.GetChannelVideosAsync(channel2.Url, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("channel2-called"))
            .ReturnsAsync(new List<YtDlpMetadata>());

        // Should not throw
        var results = await _service.RefreshAllChannelsAsync();

        // Both channels should have been attempted
        callOrder.Should().Contain(new[] { "channel1-called", "channel2-called" });
        
        // Only channel 2 should be in successful results
        results.Should().ContainSingle(r => r.ChannelId == channel2.Id);
    }

    [Fact]
    public async Task RefreshAllChannelsAsync_ShouldReturnEmptyList_WhenNoEnabledChannels()
    {
        _testChannel.Enabled = false;
        _context.SaveChanges();

        var results = await _service.RefreshAllChannelsAsync();

        results.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static YtDlpMetadata CreateVideoMetadata(
        string id,
        string title,
        DateTimeOffset? publishedAt = null,
        int? duration = null,
        string? description = null,
        string? thumbnail = null)
    {
        return new YtDlpMetadata
        {
            Id = id,
            Title = title,
            Description = description ?? $"Description for {title}",
            Duration = duration ?? 300,
            Thumbnail = thumbnail ?? $"https://example.com/thumb/{id}.jpg",
            Timestamp = publishedAt?.ToUnixTimeSeconds(),
            Channel = "Test Channel",
            ChannelId = "test-channel-yt-id"
        };
    }

    #endregion
}