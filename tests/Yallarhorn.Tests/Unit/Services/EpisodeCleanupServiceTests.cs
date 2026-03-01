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
using Yallarhorn.Services;

public class EpisodeCleanupServiceTests : IDisposable
{
    private readonly YallarhornDbContext _context;
    private readonly EpisodeRepository _episodeRepository;
    private readonly ChannelRepository _channelRepository;
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly EpisodeCleanupService _service;
    private readonly string _testDownloadDir;
    private readonly Channel _testChannel;

    public EpisodeCleanupServiceTests()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"yallarhorn_cleanup_test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        _context = new YallarhornDbContext(options);
        _context.Database.EnsureCreated();
        _episodeRepository = new EpisodeRepository(_context);
        _channelRepository = new ChannelRepository(_context);
        _fileServiceMock = new Mock<IFileService>();
        _testDownloadDir = Path.Combine(Path.GetTempPath(), $"yallarhorn_downloads_{Guid.NewGuid()}");

        var yallarhornOptions = Options.Create(new YallarhornOptions
        {
            DownloadDir = _testDownloadDir,
            TempDir = Path.Combine(Path.GetTempPath(), $"yallarhorn_temp_{Guid.NewGuid()}")
        });

        _service = new EpisodeCleanupService(
            _episodeRepository,
            _channelRepository,
            _fileServiceMock.Object,
            yallarhornOptions);

        _testChannel = new Channel
        {
            Id = "cleanup-test-channel",
            Url = "https://www.youtube.com/@cleanuptest",
            Title = "Cleanup Test Channel",
            EpisodeCountConfig = 3, // Keep only 3 episodes
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
        if (Directory.Exists(_testDownloadDir))
        {
            Directory.Delete(_testDownloadDir, true);
        }
    }

    #region CleanupChannelAsync Tests

    [Fact]
    public async Task CleanupChannelAsync_ShouldReturnEmptyResult_WhenChannelNotFound()
    {
        var result = await _service.CleanupChannelAsync("nonexistent-channel");

        result.EpisodesRemoved.Should().Be(0);
        result.BytesFreed.Should().Be(0);
    }

    [Fact]
    public async Task CleanupChannelAsync_ShouldReturnEmptyResult_WhenNoEpisodesToCleanup()
    {
        // Create only 2 episodes (within the limit of 3)
        CreateEpisode("ep1", "vid1", DateTimeOffset.UtcNow.AddDays(-1));
        CreateEpisode("ep2", "vid2", DateTimeOffset.UtcNow.AddDays(-2));

        var result = await _service.CleanupChannelAsync(_testChannel.Id);

        result.EpisodesRemoved.Should().Be(0);
        result.BytesFreed.Should().Be(0);
    }

    [Fact]
    public async Task CleanupChannelAsync_ShouldRemoveEpisodesOutsideRollingWindow()
    {
        // Create 5 episodes with different dates (channel keeps only 3)
        CreateCompletedEpisode("ep1", "vid1", DateTimeOffset.UtcNow.AddDays(-5), 1024, 2048);
        CreateCompletedEpisode("ep2", "vid2", DateTimeOffset.UtcNow.AddDays(-4), 1024, 2048);
        CreateCompletedEpisode("ep3", "vid3", DateTimeOffset.UtcNow.AddDays(-3), 1024, 2048);
        CreateCompletedEpisode("ep4", "vid4", DateTimeOffset.UtcNow.AddDays(-2), 1024, 2048);
        CreateCompletedEpisode("ep5", "vid5", DateTimeOffset.UtcNow.AddDays(-1), 1024, 2048);

        var result = await _service.CleanupChannelAsync(_testChannel.Id);

        // Should remove the 2 oldest episodes (ep1 and ep2)
        result.EpisodesRemoved.Should().Be(2);
        // 2 episodes * (1024 audio + 2048 video) = 6144 bytes
        result.BytesFreed.Should().Be(6144);
    }

    [Fact]
    public async Task CleanupChannelAsync_ShouldKeepTopNEpisodesByPublishedAt()
    {
        // Create 5 episodes with different dates (channel keeps only 3)
        var ep1 = CreateCompletedEpisode("ep1", "vid1", DateTimeOffset.UtcNow.AddDays(-5), 1024, 2048);
        var ep2 = CreateCompletedEpisode("ep2", "vid2", DateTimeOffset.UtcNow.AddDays(-4), 1024, 2048);
        var ep3 = CreateCompletedEpisode("ep3", "vid3", DateTimeOffset.UtcNow.AddDays(-3), 1024, 2048);
        var ep4 = CreateCompletedEpisode("ep4", "vid4", DateTimeOffset.UtcNow.AddDays(-2), 1024, 2048);
        var ep5 = CreateCompletedEpisode("ep5", "vid5", DateTimeOffset.UtcNow.AddDays(-1), 1024, 2048);

        await _service.CleanupChannelAsync(_testChannel.Id);

        // Verify the oldest 2 are marked as deleted
        var deletedEpisodes = await _episodeRepository.GetByStatusAsync(EpisodeStatus.Deleted);
        deletedEpisodes.Should().HaveCount(2);
        deletedEpisodes.Select(e => e.Id).Should().Contain(new[] { "ep1", "ep2" });

        // Verify the newest 3 are still completed
        var completedEpisodes = await _episodeRepository.GetByStatusAsync(EpisodeStatus.Completed);
        completedEpisodes.Should().HaveCount(3);
        completedEpisodes.Select(e => e.Id).Should().Contain(new[] { "ep3", "ep4", "ep5" });
    }

    [Fact]
    public async Task CleanupChannelAsync_ShouldNotRemoveNonCompletedEpisodes()
    {
        // Create only 4 completed episodes (keeps 3, removes 1)
        CreateCompletedEpisode("ep1", "vid1", DateTimeOffset.UtcNow.AddDays(-5), 1024, 2048);
        CreateCompletedEpisode("ep2", "vid2", DateTimeOffset.UtcNow.AddDays(-4), 1024, 2048);
        CreateCompletedEpisode("ep3", "vid3", DateTimeOffset.UtcNow.AddDays(-3), 1024, 2048);
        CreateCompletedEpisode("ep4", "vid4", DateTimeOffset.UtcNow.AddDays(-2), 1024, 2048);

        // Create a pending episode (should not be affected)
        CreateEpisode("ep-pending", "vid-pending", DateTimeOffset.UtcNow.AddDays(-10), EpisodeStatus.Pending);

        var result = await _service.CleanupChannelAsync(_testChannel.Id);

        // Should only remove completed episodes outside window (ep1 is oldest completed)
        // Channel keeps 3, has 4 completed, removes 1
        result.EpisodesRemoved.Should().Be(1);

        // The pending episode should still be pending
        var pending = await _episodeRepository.GetByVideoIdAsync("vid-pending");
        pending!.Status.Should().Be(EpisodeStatus.Pending);
    }

    [Fact]
    public async Task CleanupChannelAsync_ShouldDeleteFilesFromDisk()
    {
        // Create episodes with file paths
        var ep1 = CreateCompletedEpisode("ep1", "vid1", DateTimeOffset.UtcNow.AddDays(-5), 1024, 2048,
            "cleanup-test-channel/audio/vid1.mp3", "cleanup-test-channel/video/vid1.mp4", "cleanup-test-channel/thumbnails/vid1.jpg");
        CreateCompletedEpisode("ep2", "vid2", DateTimeOffset.UtcNow.AddDays(-4), 1024, 2048);
        CreateCompletedEpisode("ep3", "vid3", DateTimeOffset.UtcNow.AddDays(-3), 1024, 2048);
        CreateCompletedEpisode("ep4", "vid4", DateTimeOffset.UtcNow.AddDays(-2), 1024, 2048);
        CreateCompletedEpisode("ep5", "vid5", DateTimeOffset.UtcNow.AddDays(-1), 1024, 2048);

        await _service.CleanupChannelAsync(_testChannel.Id);

        // Verify file deletion was called for ep1 (the oldest)
        _fileServiceMock.Verify(f => f.DeleteFile(
            Path.Combine(_testDownloadDir, "cleanup-test-channel/audio/vid1.mp3")), Times.Once);
        _fileServiceMock.Verify(f => f.DeleteFile(
            Path.Combine(_testDownloadDir, "cleanup-test-channel/video/vid1.mp4")), Times.Once);
        _fileServiceMock.Verify(f => f.DeleteFile(
            Path.Combine(_testDownloadDir, "cleanup-test-channel/thumbnails/vid1.jpg")), Times.Once);
    }

    [Fact]
    public async Task CleanupChannelAsync_ShouldContinueIfFileDeletionFails()
    {
        // Create only 4 completed episodes (keeps 3, removes 1)
        CreateCompletedEpisode("ep1", "vid1", DateTimeOffset.UtcNow.AddDays(-5), 1024, 2048);
        CreateCompletedEpisode("ep2", "vid2", DateTimeOffset.UtcNow.AddDays(-4), 1024, 2048);
        CreateCompletedEpisode("ep3", "vid3", DateTimeOffset.UtcNow.AddDays(-3), 1024, 2048);
        CreateCompletedEpisode("ep4", "vid4", DateTimeOffset.UtcNow.AddDays(-2), 1024, 2048);

        // Setup mock to throw on file deletion
        _fileServiceMock.Setup(f => f.DeleteFile(It.IsAny<string>()))
            .Throws(new IOException("File not found"));

        // Should not throw
        var result = await _service.CleanupChannelAsync(_testChannel.Id);

        // Should still mark episodes as deleted even if file deletion failed
        result.EpisodesRemoved.Should().Be(1);
    }

    [Fact]
    public async Task CleanupChannelAsync_ShouldMarkEpisodesAsDeleted()
    {
        CreateCompletedEpisode("ep1", "vid1", DateTimeOffset.UtcNow.AddDays(-5), 1024, 2048);
        CreateCompletedEpisode("ep2", "vid2", DateTimeOffset.UtcNow.AddDays(-4), 1024, 2048);
        CreateCompletedEpisode("ep3", "vid3", DateTimeOffset.UtcNow.AddDays(-3), 1024, 2048);
        CreateCompletedEpisode("ep4", "vid4", DateTimeOffset.UtcNow.AddDays(-2), 1024, 2048);
        CreateCompletedEpisode("ep5", "vid5", DateTimeOffset.UtcNow.AddDays(-1), 1024, 2048);

        await _service.CleanupChannelAsync(_testChannel.Id);

        var deletedEpisode = await _episodeRepository.GetByVideoIdAsync("vid1");
        deletedEpisode!.Status.Should().Be(EpisodeStatus.Deleted);
    }

    [Fact]
    public async Task CleanupChannelAsync_ShouldHandleNullFilePaths()
    {
        // Episode with no video file (only audio)
        // Use "" to explicitly set null for video path
        // Create only 4 episodes (keeps 3, removes 1)
        CreateCompletedEpisode("ep1", "vid1", DateTimeOffset.UtcNow.AddDays(-5), 1024, null,
            "cleanup-test-channel/audio/vid1.mp3", "", "");
        CreateCompletedEpisode("ep2", "vid2", DateTimeOffset.UtcNow.AddDays(-4), 1024, 2048);
        CreateCompletedEpisode("ep3", "vid3", DateTimeOffset.UtcNow.AddDays(-3), 1024, 2048);
        CreateCompletedEpisode("ep4", "vid4", DateTimeOffset.UtcNow.AddDays(-2), 1024, 2048);

        var result = await _service.CleanupChannelAsync(_testChannel.Id);

        // Should handle null file paths gracefully
        result.EpisodesRemoved.Should().Be(1);
        // Only audio bytes: 1024 (video was null)
        result.BytesFreed.Should().Be(1024);

        // Should only delete the audio file (video path is null)
        _fileServiceMock.Verify(f => f.DeleteFile(
            Path.Combine(_testDownloadDir, "cleanup-test-channel/audio/vid1.mp3")), Times.Once);
        // Video file should not be attempted since FilePathVideo is null
        _fileServiceMock.Verify(f => f.DeleteFile(
            It.Is<string>(s => s.Contains("video"))), Times.Never);
    }

    [Fact]
    public async Task CleanupChannelAsync_ShouldCalculateStorageFreedCorrectly()
    {
        // Create episodes with different sizes
        CreateCompletedEpisode("ep1", "vid1", DateTimeOffset.UtcNow.AddDays(-5), 5000, 10000);
        CreateCompletedEpisode("ep2", "vid2", DateTimeOffset.UtcNow.AddDays(-4), 3000, 6000);
        CreateCompletedEpisode("ep3", "vid3", DateTimeOffset.UtcNow.AddDays(-3), 1024, 2048);
        CreateCompletedEpisode("ep4", "vid4", DateTimeOffset.UtcNow.AddDays(-2), 1024, 2048);
        CreateCompletedEpisode("ep5", "vid5", DateTimeOffset.UtcNow.AddDays(-1), 1024, 2048);

        var result = await _service.CleanupChannelAsync(_testChannel.Id);

        // Sum of audio + video for the oldest 2 episodes
        // ep1: 5000 + 10000 = 15000
        // ep2: 3000 + 6000 = 9000
        // Total: 24000
        result.BytesFreed.Should().Be(24000);
    }

    [Fact]
    public async Task CleanupChannelAsync_ShouldNotDeleteAlreadyDeletedEpisodes()
    {
        // Create a deleted episode
        var deletedEp = CreateEpisode("ep-deleted", "vid-deleted", DateTimeOffset.UtcNow.AddDays(-10), EpisodeStatus.Deleted);
        deletedEp.FileSizeAudio = 9999;
        deletedEp.FileSizeVideo = 9999;
        _context.SaveChanges();

        // Create only 4 completed episodes (keeps 3, removes 1)
        CreateCompletedEpisode("ep1", "vid1", DateTimeOffset.UtcNow.AddDays(-5), 1024, 2048);
        CreateCompletedEpisode("ep2", "vid2", DateTimeOffset.UtcNow.AddDays(-4), 1024, 2048);
        CreateCompletedEpisode("ep3", "vid3", DateTimeOffset.UtcNow.AddDays(-3), 1024, 2048);
        CreateCompletedEpisode("ep4", "vid4", DateTimeOffset.UtcNow.AddDays(-2), 1024, 2048);

        var result = await _service.CleanupChannelAsync(_testChannel.Id);

        // The already-deleted episode should not be included in the count
        result.EpisodesRemoved.Should().Be(1);
        result.BytesFreed.Should().Be(3072); // Only ep1: 1024 + 2048
    }

    #endregion

    #region CleanupAllChannelsAsync Tests

    [Fact]
    public async Task CleanupAllChannelsAsync_ShouldCleanupAllEnabledChannels()
    {
        // Create another enabled channel
        var channel2 = new Channel
        {
            Id = "cleanup-test-channel-2",
            Url = "https://www.youtube.com/@cleanuptest2",
            Title = "Cleanup Test Channel 2",
            EpisodeCountConfig = 2,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Channels.Add(channel2);
        _context.SaveChanges();

        // Add episodes to first channel
        CreateCompletedEpisode("ep1", "vid1", DateTimeOffset.UtcNow.AddDays(-5), 1024, 2048, channelId: _testChannel.Id);
        CreateCompletedEpisode("ep2", "vid2", DateTimeOffset.UtcNow.AddDays(-4), 1024, 2048, channelId: _testChannel.Id);
        CreateCompletedEpisode("ep3", "vid3", DateTimeOffset.UtcNow.AddDays(-3), 1024, 2048, channelId: _testChannel.Id);
        CreateCompletedEpisode("ep4", "vid4", DateTimeOffset.UtcNow.AddDays(-2), 1024, 2048, channelId: _testChannel.Id);

        // Add episodes to second channel
        CreateCompletedEpisode("ep5", "vid5", DateTimeOffset.UtcNow.AddDays(-5), 2048, 4096, channelId: channel2.Id);
        CreateCompletedEpisode("ep6", "vid6", DateTimeOffset.UtcNow.AddDays(-4), 2048, 4096, channelId: channel2.Id);
        CreateCompletedEpisode("ep7", "vid7", DateTimeOffset.UtcNow.AddDays(-3), 2048, 4096, channelId: channel2.Id);

        var results = (await _service.CleanupAllChannelsAsync()).ToList();

        results.Should().HaveCount(2);

        // Channel 1: keeps 3, has 4, removes 1
        var channel1Result = results.FirstOrDefault(r => r.ChannelId == _testChannel.Id);
        channel1Result.Should().NotBeNull();
        channel1Result!.EpisodesRemoved.Should().Be(1);
        channel1Result.BytesFreed.Should().Be(3072);

        // Channel 2: keeps 2, has 3, removes 1
        var channel2Result = results.FirstOrDefault(r => r.ChannelId == channel2.Id);
        channel2Result.Should().NotBeNull();
        channel2Result!.EpisodesRemoved.Should().Be(1);
        channel2Result.BytesFreed.Should().Be(6144);
    }

    [Fact]
    public async Task CleanupAllChannelsAsync_ShouldSkipDisabledChannels()
    {
        // Create a disabled channel
        var disabledChannel = new Channel
        {
            Id = "disabled-channel",
            Url = "https://www.youtube.com/@disabled",
            Title = "Disabled Channel",
            EpisodeCountConfig = 2,
            Enabled = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Channels.Add(disabledChannel);
        _context.SaveChanges();

        // Add episodes that should NOT be cleaned up
        CreateCompletedEpisode("ep-disabled-1", "vid-disabled-1", DateTimeOffset.UtcNow.AddDays(-10), 10000, 20000, channelId: disabledChannel.Id);
        CreateCompletedEpisode("ep-disabled-2", "vid-disabled-2", DateTimeOffset.UtcNow.AddDays(-9), 10000, 20000, channelId: disabledChannel.Id);
        CreateCompletedEpisode("ep-disabled-3", "vid-disabled-3", DateTimeOffset.UtcNow.AddDays(-8), 10000, 20000, channelId: disabledChannel.Id);

        var results = await _service.CleanupAllChannelsAsync();

        // Disabled channel should not appear in results
        results.Should().NotContain(r => r.ChannelId == disabledChannel.Id);
    }

    [Fact]
    public async Task CleanupAllChannelsAsync_ShouldReturnEmptyList_WhenNoEnabledChannels()
    {
        // Disable the test channel
        _testChannel.Enabled = false;
        _context.SaveChanges();

        var results = await _service.CleanupAllChannelsAsync();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task CleanupAllChannelsAsync_ShouldContinueOnError()
    {
        // Create another channel that will be cleaned
        var channel2 = new Channel
        {
            Id = "cleanup-test-channel-2",
            Url = "https://www.youtube.com@cleanuptest2",
            Title = "Cleanup Test Channel 2",
            EpisodeCountConfig = 2,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Channels.Add(channel2);
        _context.SaveChanges();

        // Add episodes to first channel (will process fine)
        CreateCompletedEpisode("ep1", "vid1", DateTimeOffset.UtcNow.AddDays(-5), 1024, 2048, channelId: _testChannel.Id);
        CreateCompletedEpisode("ep2", "vid2", DateTimeOffset.UtcNow.AddDays(-4), 1024, 2048, channelId: _testChannel.Id);
        CreateCompletedEpisode("ep3", "vid3", DateTimeOffset.UtcNow.AddDays(-3), 1024, 2048, channelId: _testChannel.Id);
        CreateCompletedEpisode("ep4", "vid4", DateTimeOffset.UtcNow.AddDays(-2), 1024, 2048, channelId: _testChannel.Id);

        // Add episodes to second channel
        CreateCompletedEpisode("ep5", "vid5", DateTimeOffset.UtcNow.AddDays(-5), 2048, 4096, channelId: channel2.Id);
        CreateCompletedEpisode("ep6", "vid6", DateTimeOffset.UtcNow.AddDays(-4), 2048, 4096, channelId: channel2.Id);
        CreateCompletedEpisode("ep7", "vid7", DateTimeOffset.UtcNow.AddDays(-3), 2048, 4096, channelId: channel2.Id);

        // Make file deletion throw for first channel (simulating error)
        var callCount = 0;
        _fileServiceMock.Setup(f => f.DeleteFile(It.IsAny<string>()))
            .Callback(() =>
            {
                callCount++;
                if (callCount <= 3) // Throw on first episode's files
                {
                    throw new IOException("Simulated error");
                }
            });

        // Should not throw
        var results = await _service.CleanupAllChannelsAsync();

        // Both channels should be processed
        results.Should().HaveCount(2);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CleanupChannelAsync_ShouldHandleEpisodesWithNullPublishedAt()
    {
        // Create only 4 episodes (keeps 3, removes 1)
        // Episode with no published_at (should be treated as oldest due to DateTimeOffset.MinValue in ordering)
        CreateCompletedEpisode("ep-no-date", "vid-no-date", null, 1024, 2048);
        CreateCompletedEpisode("ep1", "vid1", DateTimeOffset.UtcNow.AddDays(-3), 1024, 2048);
        CreateCompletedEpisode("ep2", "vid2", DateTimeOffset.UtcNow.AddDays(-2), 1024, 2048);
        CreateCompletedEpisode("ep3", "vid3", DateTimeOffset.UtcNow.AddDays(-1), 1024, 2048);

        var result = await _service.CleanupChannelAsync(_testChannel.Id);

        // Should remove the episode with no date (treated as oldest since PublishedAt is null/MinValue)
        result.EpisodesRemoved.Should().Be(1);

        var deleted = await _episodeRepository.GetByVideoIdAsync("vid-no-date");
        deleted!.Status.Should().Be(EpisodeStatus.Deleted);
    }

    [Fact]
    public async Task CleanupChannelAsync_ShouldHandleChannelWithZeroEpisodeCount()
    {
        _testChannel.EpisodeCountConfig = 0;
        _context.SaveChanges();

        CreateCompletedEpisode("ep1", "vid1", DateTimeOffset.UtcNow.AddDays(-1), 1024, 2048);

        var result = await _service.CleanupChannelAsync(_testChannel.Id);

        // With 0 episode count, should remove all completed episodes
        result.EpisodesRemoved.Should().Be(1);
    }

    [Fact]
    public async Task CleanupChannelAsync_ShouldHandleMissingThumbnail()
    {
        // Create only 4 episodes (keeps 3, removes 1)
        // Use "" to explicitly set null for thumbnail path
        CreateCompletedEpisode("ep1", "vid1", DateTimeOffset.UtcNow.AddDays(-5), 1024, 2048,
            "channel/audio/vid1.mp3", "channel/video/vid1.mp4", "");
        CreateCompletedEpisode("ep2", "vid2", DateTimeOffset.UtcNow.AddDays(-4), 1024, 2048);
        CreateCompletedEpisode("ep3", "vid3", DateTimeOffset.UtcNow.AddDays(-3), 1024, 2048);
        CreateCompletedEpisode("ep4", "vid4", DateTimeOffset.UtcNow.AddDays(-2), 1024, 2048);

        var result = await _service.CleanupChannelAsync(_testChannel.Id);

        // Should handle null thumbnail gracefully
        result.EpisodesRemoved.Should().Be(1);

        // Should not attempt to delete thumbnail since ThumbnailUrl is null
        _fileServiceMock.Verify(f => f.DeleteFile(
            It.Is<string>(s => s.Contains("thumbnails"))), Times.Never);
    }

    #endregion

    #region Helper Methods

    private Episode CreateEpisode(
        string id,
        string videoId,
        DateTimeOffset? publishedAt,
        EpisodeStatus status = EpisodeStatus.Completed,
        string channelId = null!)
    {
        var episode = new Episode
        {
            Id = id,
            VideoId = videoId,
            ChannelId = channelId ?? _testChannel.Id,
            Title = $"Episode {id}",
            PublishedAt = publishedAt,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.Add(episode);
        _context.SaveChanges();
        _context.Entry(episode).State = EntityState.Detached;
        return episode;
    }

    private Episode CreateCompletedEpisode(
        string id,
        string videoId,
        DateTimeOffset? publishedAt,
        long? fileSizeAudio,
        long? fileSizeVideo,
        string? filePathAudio = null,
        string? filePathVideo = null,
        string? thumbnailPath = null,
        string channelId = null!)
    {
        // Use empty string as sentinel to explicitly set null
        // null means "use default"
        // "" means "set to null"
        // any other value means "use that value"
        var audioPath = filePathAudio == "" ? null : filePathAudio ?? $"cleanup-test-channel/audio/{videoId}.mp3";
        var videoPath = filePathVideo == "" ? null : filePathVideo ?? $"cleanup-test-channel/video/{videoId}.mp4";
        var thumbPath = thumbnailPath == "" ? null : thumbnailPath ?? $"cleanup-test-channel/thumbnails/{videoId}.jpg";

        var episode = new Episode
        {
            Id = id,
            VideoId = videoId,
            ChannelId = channelId ?? _testChannel.Id,
            Title = $"Episode {id}",
            PublishedAt = publishedAt,
            Status = EpisodeStatus.Completed,
            DownloadedAt = publishedAt,
            FilePathAudio = audioPath,
            FilePathVideo = videoPath,
            ThumbnailUrl = thumbPath,
            FileSizeAudio = fileSizeAudio,
            FileSizeVideo = fileSizeVideo,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.Add(episode);
        _context.SaveChanges();
        _context.Entry(episode).State = EntityState.Detached;
        return episode;
    }

    #endregion
}