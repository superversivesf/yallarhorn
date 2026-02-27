namespace Yallarhorn.Tests.Unit.Data.Repositories;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Yallarhorn.Data;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;

public class EpisodeRepositoryTests : IDisposable
{
    private readonly YallarhornDbContext _context;
    private readonly EpisodeRepository _repository;
    private readonly Channel _testChannel;

    public EpisodeRepositoryTests()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"yallarhorn_episode_test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        _context = new YallarhornDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new EpisodeRepository(_context);

        _testChannel = new Channel
        {
            Id = "ep-test-channel",
            Url = "https://www.youtube.com/@eptest",
            Title = "EP Test Channel",
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

    [Fact]
    public async Task GetByVideoIdAsync_ShouldReturnEpisodeWithChannel()
    {
        var episode = new Episode
        {
            Id = "ep-vid-1",
            VideoId = "vid123",
            ChannelId = _testChannel.Id,
            Title = "Video Episode",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.Add(episode);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByVideoIdAsync("vid123");

        result.Should().NotBeNull();
        result!.Channel.Should().NotBeNull();
        result.Channel!.Title.Should().Be("EP Test Channel");
    }

    [Fact]
    public async Task GetByChannelIdAsync_ShouldReturnEpisodesOrderedByPublishedAt()
    {
        var episodes = new[]
        {
            new Episode { Id = "ep-ch-1", VideoId = "ch1", ChannelId = _testChannel.Id, Title = "Episode 1", PublishedAt = DateTimeOffset.UtcNow.AddDays(-2), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Episode { Id = "ep-ch-2", VideoId = "ch2", ChannelId = _testChannel.Id, Title = "Episode 2", PublishedAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Episode { Id = "ep-ch-3", VideoId = "ch3", ChannelId = _testChannel.Id, Title = "Episode 3", PublishedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        };
        _context.Episodes.AddRange(episodes);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByChannelIdAsync(_testChannel.Id);

        result.Should().HaveCount(3);
        result.First().Title.Should().Be("Episode 3");
    }

    [Fact]
    public async Task GetByChannelIdAsync_WithLimit_ShouldReturnLimitedEpisodes()
    {
        var episodes = Enumerable.Range(1, 10).Select(i => new Episode
        {
            Id = $"ep-limit-{i}",
            VideoId = $"limit{i}",
            ChannelId = _testChannel.Id,
            Title = $"Limit Episode {i}",
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-i),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        _context.Episodes.AddRange(episodes);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByChannelIdAsync(_testChannel.Id, 5);

        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetByStatusAsync_ShouldReturnMatchingEpisodes()
    {
        var episodes = new[]
        {
            new Episode { Id = "ep-st-1", VideoId = "st1", ChannelId = _testChannel.Id, Title = "Pending", Status = EpisodeStatus.Pending, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Episode { Id = "ep-st-2", VideoId = "st2", ChannelId = _testChannel.Id, Title = "Completed", Status = EpisodeStatus.Completed, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Episode { Id = "ep-st-3", VideoId = "st3", ChannelId = _testChannel.Id, Title = "Failed", Status = EpisodeStatus.Failed, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        };
        _context.Episodes.AddRange(episodes);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByStatusAsync(EpisodeStatus.Completed);

        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Completed");
    }

    [Fact]
    public async Task GetDownloadedAsync_Audio_ShouldReturnEpisodesWithAudioFiles()
    {
        var episodes = new[]
        {
            new Episode { Id = "ep-dl-1", VideoId = "dl1", ChannelId = _testChannel.Id, Title = "Audio", Status = EpisodeStatus.Completed, FilePathAudio = "/audio.mp3", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Episode { Id = "ep-dl-2", VideoId = "dl2", ChannelId = _testChannel.Id, Title = "Video Only", Status = EpisodeStatus.Completed, FilePathVideo = "/video.mp4", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Episode { Id = "ep-dl-3", VideoId = "dl3", ChannelId = _testChannel.Id, Title = "Pending", Status = EpisodeStatus.Pending, FilePathAudio = "/pending.mp3", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        };
        _context.Episodes.AddRange(episodes);
        await _context.SaveChangesAsync();

        var result = await _repository.GetDownloadedAsync(_testChannel.Id, FeedType.Audio, 10);

        result.Should().HaveCount(1);
        result.First().Title.Should().Be("Audio");
    }

    [Fact]
    public async Task CountByChannelIdAsync_ShouldReturnCorrectCount()
    {
        var episodes = Enumerable.Range(1, 5).Select(i => new Episode
        {
            Id = $"ep-cnt-{i}",
            VideoId = $"cnt{i}",
            ChannelId = _testChannel.Id,
            Title = $"Count {i}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        _context.Episodes.AddRange(episodes);
        await _context.SaveChangesAsync();

        var result = await _repository.CountByChannelIdAsync(_testChannel.Id);

        result.Should().Be(5);
    }

    [Fact]
    public async Task GetOldestByChannelIdAsync_ShouldReturnOldestEpisodes()
    {
        var episodes = Enumerable.Range(1, 10).Select(i => new Episode
        {
            Id = $"ep-old-{i}",
            VideoId = $"old{i}",
            ChannelId = _testChannel.Id,
            Title = $"Old {i}",
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-i),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        _context.Episodes.AddRange(episodes);
        await _context.SaveChangesAsync();

        var result = await _repository.GetOldestByChannelIdAsync(_testChannel.Id, 3);

        result.Should().HaveCount(3);
        result.First().PublishedAt!.Value.Should().BeBefore(result.Last()!.PublishedAt!.Value);
    }

    [Fact]
    public async Task ExistsByVideoIdAsync_ShouldReturnCorrectValue()
    {
        var episode = new Episode
        {
            Id = "ep-ex-1",
            VideoId = "exists123",
            ChannelId = _testChannel.Id,
            Title = "Exists Test",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.Add(episode);
        await _context.SaveChangesAsync();

        var exists = await _repository.ExistsByVideoIdAsync("exists123");
        var notExists = await _repository.ExistsByVideoIdAsync("notexists");

        exists.Should().BeTrue();
        notExists.Should().BeFalse();
    }
}