namespace Yallarhorn.Tests.Unit.Data.Repositories;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Yallarhorn.Data;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;

public class ChannelRepositoryTests : IDisposable
{
    private readonly YallarhornDbContext _context;
    private readonly ChannelRepository _repository;

    public ChannelRepositoryTests()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"yallarhorn_channel_test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        _context = new YallarhornDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new ChannelRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnChannel()
    {
        var channel = new Channel
        {
            Id = "repo-test-1",
            Url = "https://www.youtube.com/@repotest1",
            Title = "Repo Test 1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _repository.AddAsync(channel);

        var result = await _repository.GetByIdAsync("repo-test-1");

        result.Should().NotBeNull();
        result!.Title.Should().Be("Repo Test 1");
    }

    [Fact]
    public async Task GetByUrlAsync_ShouldReturnChannelWithEpisodes()
    {
        var channel = new Channel
        {
            Id = "url-test-1",
            Url = "https://www.youtube.com/@urltest",
            Title = "URL Test",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _repository.AddAsync(channel);

        var episode = new Episode
        {
            Id = "url-ep-1",
            VideoId = "urlvid123",
            ChannelId = channel.Id,
            Title = "URL Episode",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Episodes.Add(episode);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByUrlAsync("https://www.youtube.com/@urltest");

        result.Should().NotBeNull();
        result!.Episodes.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetEnabledAsync_ShouldReturnOnlyEnabledChannels()
    {
        var channel1 = new Channel
        {
            Id = "enabled-1",
            Url = "https://www.youtube.com/@enabled1",
            Title = "Enabled 1",
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var channel2 = new Channel
        {
            Id = "disabled-1",
            Url = "https://www.youtube.com/@disabled1",
            Title = "Disabled 1",
            Enabled = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _repository.AddRangeAsync(new[] { channel1, channel2 });

        var result = await _repository.GetEnabledAsync();

        result.Should().HaveCount(1);
        result.First().Id.Should().Be("enabled-1");
    }

    [Fact]
    public async Task GetChannelsNeedingRefreshAsync_ShouldReturnCorrectChannels()
    {
        var channel1 = new Channel
        {
            Id = "refresh-1",
            Url = "https://www.youtube.com/@refresh1",
            Title = "Refresh 1",
            Enabled = true,
            LastRefreshAt = DateTimeOffset.UtcNow - TimeSpan.FromHours(2),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var channel2 = new Channel
        {
            Id = "refresh-2",
            Url = "https://www.youtube.com/@refresh2",
            Title = "Refresh 2",
            Enabled = true,
            LastRefreshAt = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(30),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var channel3 = new Channel
        {
            Id = "refresh-3",
            Url = "https://www.youtube.com/@refresh3",
            Title = "Refresh 3",
            Enabled = true,
            LastRefreshAt = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _repository.AddRangeAsync(new[] { channel1, channel2, channel3 });

        var result = await _repository.GetChannelsNeedingRefreshAsync(TimeSpan.FromHours(1));

        result.Should().HaveCount(2);
        result.Select(c => c.Id).Should().Contain(new[] { "refresh-1", "refresh-3" });
    }

    [Fact]
    public async Task ExistsByUrlAsync_ShouldReturnCorrectValue()
    {
        var channel = new Channel
        {
            Id = "exists-1",
            Url = "https://www.youtube.com/@exists1",
            Title = "Exists 1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _repository.AddAsync(channel);

        var exists = await _repository.ExistsByUrlAsync("https://www.youtube.com/@exists1");
        var notExists = await _repository.ExistsByUrlAsync("https://www.youtube.com/@notexists");

        exists.Should().BeTrue();
        notExists.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateChannel()
    {
        var channel = new Channel
        {
            Id = "update-1",
            Url = "https://www.youtube.com/@update1",
            Title = "Original Title",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _repository.AddAsync(channel);

        channel.Title = "Updated Title";
        channel.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.UpdateAsync(channel);

        var result = await _repository.GetByIdAsync("update-1");
        result!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveChannel()
    {
        var channel = new Channel
        {
            Id = "delete-1",
            Url = "https://www.youtube.com/@delete1",
            Title = "Delete 1",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _repository.AddAsync(channel);

        await _repository.DeleteAsync(channel);

        var result = await _repository.GetByIdAsync("delete-1");
        result.Should().BeNull();
    }

    [Fact]
    public async Task CountAsync_ShouldReturnCorrectCount()
    {
        await _repository.AddRangeAsync(new[]
        {
            new Channel { Id = "count-1", Url = "https://www.youtube.com/@count1", Title = "Count 1", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Channel { Id = "count-2", Url = "https://www.youtube.com/@count2", Title = "Count 2", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new Channel { Id = "count-3", Url = "https://www.youtube.com/@count3", Title = "Count 3", Enabled = false, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        });

        var totalCount = await _repository.CountAsync();
        var enabledCount = await _repository.CountAsync(c => c.Enabled);

        totalCount.Should().Be(3);
        enabledCount.Should().Be(2);
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnCorrectPage()
    {
        var channels = Enumerable.Range(1, 25).Select(i => new Channel
        {
            Id = $"paged-{i}",
            Url = $"https://www.youtube.com/@paged{i}",
            Title = $"Paged {i}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _repository.AddRangeAsync(channels);

        var result = await _repository.GetPagedAsync(2, 10, orderBy: c => c.Title);

        result.Items.Should().HaveCount(10);
        result.TotalCount.Should().Be(25);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalPages.Should().Be(3);
        result.HasPrevious.Should().BeTrue();
        result.HasNext.Should().BeTrue();
    }
}