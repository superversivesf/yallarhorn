namespace Yallarhorn.Tests.Unit.Data.Repositories;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Yallarhorn.Data;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;

public class DownloadQueueRepositoryTests : IDisposable
{
    private readonly YallarhornDbContext _context;
    private readonly DownloadQueueRepository _repository;
    private readonly Channel _testChannel;
    private readonly Episode _testEpisode;

    public DownloadQueueRepositoryTests()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"yallarhorn_dq_test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        _context = new YallarhornDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new DownloadQueueRepository(_context);

        _testChannel = new Channel
        {
            Id = "dq-test-channel",
            Url = "https://www.youtube.com/@dqtest",
            Title = "DQ Test Channel",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Channels.Add(_testChannel);
        
        _testEpisode = new Episode
        {
            Id = "dq-test-episode",
            VideoId = "dqvid123",
            ChannelId = _testChannel.Id,
            Title = "DQ Test Episode",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.Add(_testEpisode);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetByEpisodeIdAsync_ShouldReturnQueueItemWithEpisode()
    {
        var queueItem = new DownloadQueue
        {
            Id = "dq-ep-1",
            EpisodeId = _testEpisode.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _repository.AddAsync(queueItem);

        var result = await _repository.GetByEpisodeIdAsync(_testEpisode.Id);

        result.Should().NotBeNull();
        result!.Episode.Should().NotBeNull();
        result.Episode!.Title.Should().Be("DQ Test Episode");
    }

    [Fact]
    public async Task GetPendingAsync_ShouldReturnOrderedByPriority()
    {
        var episode2 = new Episode
        {
            Id = "dq-ep-ep2",
            VideoId = "dqvid2",
            ChannelId = _testChannel.Id,
            Title = "Episode 2",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.Add(episode2);
        await _context.SaveChangesAsync();

        var items = new[]
        {
            new DownloadQueue { Id = "dq-pend-1", EpisodeId = _testEpisode.Id, Priority = 5, Status = QueueStatus.Pending, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new DownloadQueue { Id = "dq-pend-2", EpisodeId = episode2.Id, Priority = 1, Status = QueueStatus.Pending, CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1), UpdatedAt = DateTimeOffset.UtcNow }
        };
        await _repository.AddRangeAsync(items);
        _context.Entry(items[0]).State = EntityState.Detached;
        _context.Entry(items[1]).State = EntityState.Detached;

        var result = await _repository.GetPendingAsync();

        result.Should().HaveCount(2);
        result.First().Priority.Should().Be(1);
    }

    [Fact]
    public async Task GetPendingAsync_WithLimit_ShouldReturnLimitedItems()
    {
        var episodes = Enumerable.Range(1, 10).Select(i => new Episode
        {
            Id = $"dq-limit-ep-{i}",
            VideoId = $"limit{i}",
            ChannelId = _testChannel.Id,
            Title = $"Limit Episode {i}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        }).ToList();
        _context.Episodes.AddRange(episodes);
        await _context.SaveChangesAsync();

        var queueItems = episodes.Select((ep, i) => new DownloadQueue
        {
            Id = $"dq-limit-{i}",
            EpisodeId = ep.Id,
            Status = QueueStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _repository.AddRangeAsync(queueItems);

        var result = await _repository.GetPendingAsync(5);

        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetByStatusAsync_ShouldReturnMatchingItems()
    {
        var episode2 = new Episode
        {
            Id = "dq-st-ep-2",
            VideoId = "stvid2",
            ChannelId = _testChannel.Id,
            Title = "Status Episode 2",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.Add(episode2);
        await _context.SaveChangesAsync();

        var items = new[]
        {
            new DownloadQueue { Id = "dq-st-1", EpisodeId = _testEpisode.Id, Status = QueueStatus.Pending, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new DownloadQueue { Id = "dq-st-2", EpisodeId = episode2.Id, Status = QueueStatus.InProgress, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        };
        await _repository.AddRangeAsync(items);

        var result = await _repository.GetByStatusAsync(QueueStatus.InProgress);

        result.Should().HaveCount(1);
        result.First().Id.Should().Be("dq-st-2");
    }

    [Fact]
    public async Task GetReadyForRetryAsync_ShouldReturnItemsWithExpiredNextRetryAt()
    {
        var episode2 = new Episode
        {
            Id = "dq-rtry-ep-2",
            VideoId = "rtryvid2",
            ChannelId = _testChannel.Id,
            Title = "Retry Episode 2",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var episode3 = new Episode
        {
            Id = "dq-rtry-ep-3",
            VideoId = "rtryvid3",
            ChannelId = _testChannel.Id,
            Title = "Retry Episode 3",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.AddRange(episode2, episode3);
        await _context.SaveChangesAsync();

        var items = new[]
        {
            new DownloadQueue { Id = "dq-rtry-1", EpisodeId = _testEpisode.Id, Status = QueueStatus.Retrying, NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(-5), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new DownloadQueue { Id = "dq-rtry-2", EpisodeId = episode2.Id, Status = QueueStatus.Retrying, NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(5), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new DownloadQueue { Id = "dq-rtry-3", EpisodeId = episode3.Id, Status = QueueStatus.Pending, NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(-10), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        };
        await _repository.AddRangeAsync(items);

        var result = await _repository.GetReadyForRetryAsync();

        result.Should().HaveCount(1);
        result.First().Id.Should().Be("dq-rtry-1");
    }

    [Fact]
    public async Task CountPendingAsync_ShouldReturnCorrectCount()
    {
        var episodes = Enumerable.Range(1, 5).Select(i => new Episode
        {
            Id = $"dq-cnt-ep-{i}",
            VideoId = $"cnt{i}",
            ChannelId = _testChannel.Id,
            Title = $"Count Episode {i}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        }).ToList();
        _context.Episodes.AddRange(episodes);
        await _context.SaveChangesAsync();

        var pendingItems = episodes.Take(3).Select((ep, i) => new DownloadQueue
        {
            Id = $"dq-cnt-{i}",
            EpisodeId = ep.Id,
            Status = QueueStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        var completedItem = new DownloadQueue
        {
            Id = "dq-cnt-done",
            EpisodeId = episodes[3].Id,
            Status = QueueStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _repository.AddRangeAsync(pendingItems);
        await _repository.AddAsync(completedItem);

        var result = await _repository.CountPendingAsync();

        result.Should().Be(3);
    }

    [Fact]
    public async Task CountByStatusAsync_ShouldReturnCorrectCount()
    {
        var episodes = Enumerable.Range(1, 4).Select(i => new Episode
        {
            Id = $"dq-sts-ep-{i}",
            VideoId = $"sts{i}",
            ChannelId = _testChannel.Id,
            Title = $"Status Episode {i}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        }).ToList();
        _context.Episodes.AddRange(episodes);
        await _context.SaveChangesAsync();

        var items = new[]
        {
            new DownloadQueue { Id = "dq-sts-1", EpisodeId = episodes[0].Id, Status = QueueStatus.Failed, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new DownloadQueue { Id = "dq-sts-2", EpisodeId = episodes[1].Id, Status = QueueStatus.Failed, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new DownloadQueue { Id = "dq-sts-3", EpisodeId = episodes[2].Id, Status = QueueStatus.Completed, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new DownloadQueue { Id = "dq-sts-4", EpisodeId = episodes[3].Id, Status = QueueStatus.Failed, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        };
        await _repository.AddRangeAsync(items);

        var result = await _repository.CountByStatusAsync(QueueStatus.Failed);

        result.Should().Be(3);
    }
}