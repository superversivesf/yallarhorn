using Microsoft.EntityFrameworkCore;
using Yallarhorn.Data;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;

namespace Yallarhorn.Data.Seeding;

/// <summary>
/// Seeds development/test data into the database.
/// </summary>
public class DevelopmentSeeder
{
    private readonly YallarhornDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevelopmentSeeder"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public DevelopmentSeeder(YallarhornDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Seeds development data for testing.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.Channels.AnyAsync(cancellationToken))
        {
            return;
        }

        var channels = new[]
        {
            new Channel
            {
                Id = "dev-channel-1",
                Url = "https://www.youtube.com/@testchannel1",
                Title = "Test Channel One",
                Description = "A test channel for development",
                EpisodeCountConfig = 10,
                FeedType = FeedType.Audio,
                Enabled = true,
                LastRefreshAt = DateTimeOffset.UtcNow.AddHours(-1),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-7),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-7)
            },
            new Channel
            {
                Id = "dev-channel-2",
                Url = "https://www.youtube.com/@testchannel2",
                Title = "Test Channel Two",
                Description = "Another test channel",
                EpisodeCountConfig = 20,
                FeedType = FeedType.Video,
                Enabled = true,
                LastRefreshAt = null,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-5),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-5)
            },
            new Channel
            {
                Id = "dev-channel-3",
                Url = "https://www.youtube.com/@disabledchannel",
                Title = "Disabled Channel",
                Description = "A disabled channel",
                EpisodeCountConfig = 50,
                FeedType = FeedType.Audio,
                Enabled = false,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-3),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-3)
            }
        };

        _context.Channels.AddRange(channels);

        var episodes = new[]
        {
            new Episode
            {
                Id = "dev-episode-1",
                VideoId = "video001",
                ChannelId = "dev-channel-1",
                Title = "Test Episode 1",
                Description = "First test episode",
                DurationSeconds = 3600,
                PublishedAt = DateTimeOffset.UtcNow.AddDays(-1),
                DownloadedAt = DateTimeOffset.UtcNow.AddHours(-12),
                FilePathAudio = "/downloads/channel1/video001.mp3",
                FilePathVideo = null,
                FileSizeAudio = 50_000_000,
                FileSizeVideo = null,
                Status = EpisodeStatus.Completed,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                UpdatedAt = DateTimeOffset.UtcNow.AddHours(-12)
            },
            new Episode
            {
                Id = "dev-episode-2",
                VideoId = "video002",
                ChannelId = "dev-channel-1",
                Title = "Test Episode 2",
                Description = "Second test episode",
                DurationSeconds = 2700,
                PublishedAt = DateTimeOffset.UtcNow.AddDays(-2),
                DownloadedAt = DateTimeOffset.UtcNow.AddHours(-24),
                FilePathAudio = "/downloads/channel1/video002.mp3",
                FilePathVideo = null,
                FileSizeAudio = 45_000_000,
                FileSizeVideo = null,
                Status = EpisodeStatus.Completed,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
                UpdatedAt = DateTimeOffset.UtcNow.AddHours(-24)
            },
            new Episode
            {
                Id = "dev-episode-3",
                VideoId = "video003",
                ChannelId = "dev-channel-1",
                Title = "Pending Episode",
                Description = "Episode pending download",
                DurationSeconds = 1800,
                PublishedAt = DateTimeOffset.UtcNow.AddHours(-6),
                Status = EpisodeStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-6),
                UpdatedAt = DateTimeOffset.UtcNow.AddHours(-6)
            },
            new Episode
            {
                Id = "dev-episode-4",
                VideoId = "video004",
                ChannelId = "dev-channel-2",
                Title = "Video Episode 1",
                Description = "Video test episode",
                DurationSeconds = 5400,
                PublishedAt = DateTimeOffset.UtcNow.AddDays(-1),
                DownloadedAt = DateTimeOffset.UtcNow.AddHours(-6),
                FilePathAudio = "/downloads/channel2/video004.m4a",
                FilePathVideo = "/downloads/channel2/video004.mp4",
                FileSizeAudio = 80_000_000,
                FileSizeVideo = 500_000_000,
                Status = EpisodeStatus.Completed,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                UpdatedAt = DateTimeOffset.UtcNow.AddHours(-6)
            },
            new Episode
            {
                Id = "dev-episode-5",
                VideoId = "video005",
                ChannelId = "dev-channel-1",
                Title = "Failed Episode",
                Description = "Episode that failed to download",
                DurationSeconds = 1200,
                PublishedAt = DateTimeOffset.UtcNow.AddDays(-3),
                Status = EpisodeStatus.Failed,
                RetryCount = 3,
                ErrorMessage = "Network error: Connection timeout",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-3),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-2)
            }
        };

        _context.Episodes.AddRange(episodes);

        var queueItems = new[]
        {
            new DownloadQueue
            {
                Id = "dev-queue-1",
                EpisodeId = "dev-episode-3",
                Priority = 5,
                Status = QueueStatus.Pending,
                Attempts = 0,
                MaxAttempts = 3,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-6),
                UpdatedAt = DateTimeOffset.UtcNow.AddHours(-6)
            },
            new DownloadQueue
            {
                Id = "dev-queue-2",
                EpisodeId = "dev-episode-5",
                Priority = 3,
                Status = QueueStatus.Failed,
                Attempts = 3,
                MaxAttempts = 3,
                LastError = "Network error: Connection timeout",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-3),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-2)
            }
        };

        _context.DownloadQueue.AddRange(queueItems);

        var schemaVersion = new SchemaVersion
        {
            Version = 1,
            AppliedAt = DateTimeOffset.UtcNow,
            Description = "Initial schema"
        };

        _context.SchemaVersions.Add(schemaVersion);

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Clears all development data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _context.DownloadQueue.RemoveRange(_context.DownloadQueue);
        _context.Episodes.RemoveRange(_context.Episodes);
        _context.Channels.RemoveRange(_context.Channels);
        _context.SchemaVersions.RemoveRange(_context.SchemaVersions);
        await _context.SaveChangesAsync(cancellationToken);
    }
}