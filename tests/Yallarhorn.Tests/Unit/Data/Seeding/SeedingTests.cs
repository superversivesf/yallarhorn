namespace Yallarhorn.Tests.Unit.Data.Seeding;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Yallarhorn.Data;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Initialization;
using Yallarhorn.Data.Seeding;

public class DatabaseInitializerTests : IDisposable
{
    private readonly YallarhornDbContext _context;

    public DatabaseInitializerTests()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"yallarhorn_init_test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        _context = new YallarhornDbContext(options);
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateDatabase()
    {
        await DatabaseInitializer.InitializeAsync(_context);

        var canConnect = await _context.Database.CanConnectAsync();
        canConnect.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueAfterCreation()
    {
        await DatabaseInitializer.InitializeAsync(_context);
        var exists = await DatabaseInitializer.ExistsAsync(_context);
        exists.Should().BeTrue();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

public class DevelopmentSeederTests : IDisposable
{
    private readonly YallarhornDbContext _context;
    private readonly DevelopmentSeeder _seeder;

    public DevelopmentSeederTests()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"yallarhorn_dev_seed_test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        _context = new YallarhornDbContext(options);
        _context.Database.EnsureCreated();
        _seeder = new DevelopmentSeeder(_context);
    }

    [Fact]
    public async Task SeedAsync_ShouldAddTestData()
    {
        await _seeder.SeedAsync();

        var channels = await _context.Channels.CountAsync();
        var episodes = await _context.Episodes.CountAsync();
        var queueItems = await _context.DownloadQueue.CountAsync();

        channels.Should().Be(3);
        episodes.Should().Be(5);
        queueItems.Should().Be(2);
    }

    [Fact]
    public async Task SeedAsync_ShouldNotDuplicateData()
    {
        await _seeder.SeedAsync();
        await _seeder.SeedAsync();

        var channels = await _context.Channels.CountAsync();
        channels.Should().Be(3);
    }

    [Fact]
    public async Task SeedAsync_ShouldSetCorrectFeedType()
    {
        await _seeder.SeedAsync();

        var channel1 = await _context.Channels.FindAsync("dev-channel-1");
        var channel2 = await _context.Channels.FindAsync("dev-channel-2");

        channel1!.FeedType.Should().Be(FeedType.Audio);
        channel2!.FeedType.Should().Be(FeedType.Video);
    }

    [Fact]
    public async Task SeedAsync_ShouldSetCorrectEpisodeStatus()
    {
        await _seeder.SeedAsync();

        var completed = await _context.Episodes.CountAsync(e => e.Status == EpisodeStatus.Completed);
        var pending = await _context.Episodes.CountAsync(e => e.Status == EpisodeStatus.Pending);
        var failed = await _context.Episodes.CountAsync(e => e.Status == EpisodeStatus.Failed);

        completed.Should().Be(3);
        pending.Should().Be(1);
        failed.Should().Be(1);
    }

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllData()
    {
        await _seeder.SeedAsync();
        await _seeder.ClearAsync();

        var channels = await _context.Channels.CountAsync();
        var episodes = await _context.Episodes.CountAsync();
        var queueItems = await _context.DownloadQueue.CountAsync();

        channels.Should().Be(0);
        episodes.Should().Be(0);
        queueItems.Should().Be(0);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}

public class ChannelSeederTests : IDisposable
{
    private readonly YallarhornDbContext _context;
    private readonly ChannelSeeder _seeder;

    public ChannelSeederTests()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"yallarhorn_channel_seed_test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        _context = new YallarhornDbContext(options);
        _context.Database.EnsureCreated();
        _seeder = new ChannelSeeder(_context);
    }

    [Fact]
    public async Task SeedAsync_ShouldAddNewChannels()
    {
        var definitions = new[]
        {
            new ChannelDefinition { Url = "https://www.youtube.com/@test1", Title = "Test 1", EpisodeCount = 50, FeedType = FeedType.Audio, Enabled = true },
            new ChannelDefinition { Url = "https://www.youtube.com/@test2", Title = "Test 2", EpisodeCount = 100, FeedType = FeedType.Video, Enabled = true }
        };

        var (added, updated, skipped) = await _seeder.SeedAsync(definitions);

        added.Should().Be(2);
        updated.Should().Be(0);
        skipped.Should().Be(0);
    }

    [Fact]
    public async Task SeedAsync_ShouldSkipExistingChannelsByDefault()
    {
        var definitions = new[]
        {
            new ChannelDefinition { Url = "https://www.youtube.com/@test1", Title = "Test 1", EpisodeCount = 50, FeedType = FeedType.Audio, Enabled = true }
        };

        await _seeder.SeedAsync(definitions);
        var result = await _seeder.SeedAsync(definitions);

        result.Added.Should().Be(0);
        result.Skipped.Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_ShouldUpdateExistingChannelsWhenRequested()
    {
        var definitions = new[]
        {
            new ChannelDefinition { Url = "https://www.youtube.com/@test1", Title = "Original Title", EpisodeCount = 50, FeedType = FeedType.Audio, Enabled = true }
        };

        await _seeder.SeedAsync(definitions);

        var updateDefinitions = new[]
        {
            new ChannelDefinition { Url = "https://www.youtube.com/@test1", Title = "Updated Title", EpisodeCount = 100, FeedType = FeedType.Video, Enabled = false, UpdateIfExists = true }
        };

        var result = await _seeder.SeedAsync(updateDefinitions);

        result.Updated.Should().Be(1);

        var channel = await _context.Channels.FirstOrDefaultAsync(c => c.Url == "https://www.youtube.com/@test1");
        channel!.Title.Should().Be("Updated Title");
        channel.EpisodeCountConfig.Should().Be(100);
        channel.Enabled.Should().BeFalse();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}