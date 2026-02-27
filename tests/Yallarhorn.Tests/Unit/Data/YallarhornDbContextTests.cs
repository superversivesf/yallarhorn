namespace Yallarhorn.Tests.Unit.Data;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Yallarhorn.Data;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;

public class YallarhornDbContextTests : IDisposable
{
    private readonly YallarhornDbContext _context;

    public YallarhornDbContextTests()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"yallarhorn_test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        _context = new YallarhornDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public void DbContext_ShouldHaveChannelsDbSet()
    {
        _context.Channels.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_ShouldHaveEpisodesDbSet()
    {
        _context.Episodes.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_ShouldHaveDownloadQueueDbSet()
    {
        _context.DownloadQueue.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_ShouldHaveSchemaVersionsDbSet()
    {
        _context.SchemaVersions.Should().NotBeNull();
    }

    [Fact]
    public async Task Channel_ShouldBePersisted()
    {
        var channel = new Channel
        {
            Id = "test-channel-1",
            Url = "https://www.youtube.com/@testchannel",
            Title = "Test Channel",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Channels.Add(channel);
        await _context.SaveChangesAsync();

        var retrieved = await _context.Channels.FindAsync("test-channel-1");
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Test Channel");
    }

    [Fact]
    public async Task Episode_ShouldBePersistedWithChannelRelation()
    {
        var channel = new Channel
        {
            Id = "test-channel-2",
            Url = "https://www.youtube.com/@testchannel2",
            Title = "Test Channel 2",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Channels.Add(channel);

        var episode = new Episode
        {
            Id = "test-episode-1",
            VideoId = "abc123",
            ChannelId = channel.Id,
            Title = "Test Episode",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.Add(episode);
        await _context.SaveChangesAsync();

        var retrieved = await _context.Episodes
            .Include(e => e.Channel)
            .FirstAsync(e => e.Id == "test-episode-1");
        
        retrieved.Channel.Should().NotBeNull();
        retrieved.Channel!.Title.Should().Be("Test Channel 2");
    }

    [Fact]
    public async Task DownloadQueue_ShouldBePersistedWithEpisodeRelation()
    {
        var channel = new Channel
        {
            Id = "test-channel-3",
            Url = "https://www.youtube.com/@testchannel3",
            Title = "Test Channel 3",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Channels.Add(channel);

        var episode = new Episode
        {
            Id = "test-episode-2",
            VideoId = "def456",
            ChannelId = channel.Id,
            Title = "Test Episode 2",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.Add(episode);

        var queueItem = new DownloadQueue
        {
            Id = "queue-1",
            EpisodeId = episode.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.DownloadQueue.Add(queueItem);
        await _context.SaveChangesAsync();

        var retrieved = await _context.DownloadQueue
            .Include(dq => dq.Episode)
            .FirstAsync(dq => dq.Id == "queue-1");

        retrieved.Episode.Should().NotBeNull();
        retrieved.Episode!.Title.Should().Be("Test Episode 2");
    }

    [Fact]
    public async Task Channel_Url_ShouldBeUnique()
    {
        var channel1 = new Channel
        {
            Id = "channel-unique-1",
            Url = "https://www.youtube.com/@unique",
            Title = "Unique 1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Channels.Add(channel1);
        await _context.SaveChangesAsync();

        var channel2 = new Channel
        {
            Id = "channel-unique-2",
            Url = "https://www.youtube.com/@unique",
            Title = "Unique 2",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Channels.Add(channel2);

        var act = async () => await _context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Episode_VideoId_ShouldBeUnique()
    {
        var channel = new Channel
        {
            Id = "channel-vid-1",
            Url = "https://www.youtube.com/@videoidtest",
            Title = "Video ID Test",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Channels.Add(channel);
        await _context.SaveChangesAsync();

        var episode1 = new Episode
        {
            Id = "episode-vid-1",
            VideoId = "unique123",
            ChannelId = channel.Id,
            Title = "Episode 1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.Add(episode1);
        await _context.SaveChangesAsync();

        var episode2 = new Episode
        {
            Id = "episode-vid-2",
            VideoId = "unique123",
            ChannelId = channel.Id,
            Title = "Episode 2",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.Add(episode2);

        var act = async () => await _context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SchemaVersion_ShouldBePersisted()
    {
        var version = new SchemaVersion
        {
            Version = 1,
            AppliedAt = DateTimeOffset.UtcNow,
            Description = "Initial schema"
        };

        _context.SchemaVersions.Add(version);
        await _context.SaveChangesAsync();

        var retrieved = await _context.SchemaVersions.FindAsync(1);
        retrieved.Should().NotBeNull();
        retrieved!.Description.Should().Be("Initial schema");
    }

    [Fact]
    public async Task Channel_Deletion_ShouldCascadeEpisodes()
    {
        var channel = new Channel
        {
            Id = "cascade-channel",
            Url = "https://www.youtube.com/@cascade",
            Title = "Cascade Test",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Channels.Add(channel);

        var episode = new Episode
        {
            Id = "cascade-episode",
            VideoId = "cascade123",
            ChannelId = channel.Id,
            Title = "Cascade Episode",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.Add(episode);
        await _context.SaveChangesAsync();

        _context.Channels.Remove(channel);
        await _context.SaveChangesAsync();

        var episodeExists = await _context.Episodes.AnyAsync(e => e.Id == "cascade-episode");
        episodeExists.Should().BeFalse();
    }
}