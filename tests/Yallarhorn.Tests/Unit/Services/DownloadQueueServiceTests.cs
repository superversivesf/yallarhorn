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

public class DownloadQueueServiceTests : IDisposable
{
    private readonly YallarhornDbContext _context;
    private readonly DownloadQueueRepository _queueRepository;
    private readonly DownloadQueueService _service;
    private readonly Channel _testChannel;
    private readonly Episode _testEpisode;

    public DownloadQueueServiceTests()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"yallarhorn_queue_svc_test_{Guid.NewGuid()}.db");
        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        _context = new YallarhornDbContext(options);
        _context.Database.EnsureCreated();
        _queueRepository = new DownloadQueueRepository(_context);
        _service = new DownloadQueueService(_queueRepository);

        _testChannel = new Channel
        {
            Id = "queue-svc-channel",
            Url = "https://www.youtube.com/@queuesvctest",
            Title = "Queue Service Test Channel",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.Channels.Add(_testChannel);

        _testEpisode = new Episode
        {
            Id = "queue-svc-episode",
            VideoId = "queuesvcvid123",
            ChannelId = _testChannel.Id,
            Title = "Queue Service Test Episode",
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

    #region EnqueueAsync Tests

    [Fact]
    public async Task EnqueueAsync_ShouldCreateQueueItemWithCorrectPriority()
    {
        var result = await _service.EnqueueAsync(_testEpisode.Id, priority: 3);

        result.Should().NotBeNull();
        result.EpisodeId.Should().Be(_testEpisode.Id);
        result.Priority.Should().Be(3);
        result.Status.Should().Be(QueueStatus.Pending);
        result.Attempts.Should().Be(0);
        result.MaxAttempts.Should().Be(5);
    }

    [Fact]
    public async Task EnqueueAsync_ShouldSetDefaultPriorityTo5()
    {
        var result = await _service.EnqueueAsync(_testEpisode.Id);

        result.Priority.Should().Be(5);
    }

    [Fact]
    public async Task EnqueueAsync_ShouldThrowIfEpisodeAlreadyQueued()
    {
        await _service.EnqueueAsync(_testEpisode.Id, priority: 1);

        var act = () => _service.EnqueueAsync(_testEpisode.Id, priority: 2);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already queued*");
    }

    [Fact]
    public async Task EnqueueAsync_ShouldSetCreatedAtAndUpdatedAt()
    {
        var beforeCreate = DateTimeOffset.UtcNow;

        var result = await _service.EnqueueAsync(_testEpisode.Id);

        result.CreatedAt.Should().BeOnOrAfter(beforeCreate);
        result.UpdatedAt.Should().BeOnOrAfter(beforeCreate);
    }

    #endregion

    #region GetNextPendingAsync Tests

    [Fact]
    public async Task GetNextPendingAsync_ShouldReturnNullIfNoPendingItems()
    {
        var result = await _service.GetNextPendingAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetNextPendingAsync_ShouldOrderByPriorityThenCreatedAt()
    {
        var episode2 = CreateEpisode("ep2", "vid2");
        var episode3 = CreateEpisode("ep3", "vid3");

        // Create items with different priorities and times
        var item1 = await _service.EnqueueAsync(_testEpisode.Id, priority: 5); // Created first, mid priority
        await Task.Delay(10); // Ensure different timestamps
        var item2 = await _service.EnqueueAsync(episode2.Id, priority: 1); // Created second, highest priority
        await Task.Delay(10);
        var item3 = await _service.EnqueueAsync(episode3.Id, priority: 5); // Created third, same priority as item1

        var result = await _service.GetNextPendingAsync();

        result.Should().NotBeNull();
        result!.Id.Should().Be(item2.Id); // Priority 1 should be first
    }

    [Fact]
    public async Task GetNextPendingAsync_ShouldNotReturnInProgressItems()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.MarkInProgressAsync(item.Id);

        var result = await _service.GetNextPendingAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetNextPendingAsync_ShouldNotReturnCompletedItems()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.MarkInProgressAsync(item.Id);
        await _service.MarkCompletedAsync(item.Id);

        var result = await _service.GetNextPendingAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetNextPendingAsync_ShouldNotReturnFailedItems()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.MarkInProgressAsync(item.Id);
        await _service.MarkFailedAsync(item.Id, "Test error");

        var result = await _service.GetNextPendingAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetNextPendingAsync_ShouldNotReturnCancelledItems()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.CancelAsync(item.Id);

        var result = await _service.GetNextPendingAsync();

        result.Should().BeNull();
    }

    #endregion

    #region MarkInProgressAsync Tests

    [Fact]
    public async Task MarkInProgressAsync_ShouldTransitionFromPendingToInProgress()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);

        await _service.MarkInProgressAsync(item.Id);

        var updated = await _queueRepository.GetByIdAsync(item.Id);
        updated!.Status.Should().Be(QueueStatus.InProgress);
    }

    [Fact]
    public async Task MarkInProgressAsync_ShouldThrowIfItemNotFound()
    {
        var act = () => _service.MarkInProgressAsync("nonexistent-id");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task MarkInProgressAsync_ShouldThrowIfNotPendingOrRetrying()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.MarkInProgressAsync(item.Id);

        var act = () => _service.MarkInProgressAsync(item.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not in Pending or Retrying status*");
    }

    [Fact]
    public async Task MarkInProgressAsync_ShouldTransitionFromRetryingToInProgress()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.MarkInProgressAsync(item.Id);
        await _service.MarkFailedAsync(item.Id, "Test error");

        // Now item is in Retrying status - verify we can transition to InProgress
        var retryItem = await _queueRepository.GetByIdAsync(item.Id);
        retryItem!.Status.Should().Be(QueueStatus.Retrying);

        // Set NextRetryAt to past so it's ready for retry
        retryItem.NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await _queueRepository.UpdateAsync(retryItem);

        await _service.MarkInProgressAsync(item.Id);

        var updated = await _queueRepository.GetByIdAsync(item.Id);
        updated!.Status.Should().Be(QueueStatus.InProgress);
    }

    [Fact]
    public async Task MarkInProgressAsync_ShouldUpdateTimestamp()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        var beforeUpdate = item.UpdatedAt;

        await Task.Delay(10);
        await _service.MarkInProgressAsync(item.Id);

        var updated = await _queueRepository.GetByIdAsync(item.Id);
        updated!.UpdatedAt.Should().BeAfter(beforeUpdate);
    }

    #endregion

    #region MarkCompletedAsync Tests

    [Fact]
    public async Task MarkCompletedAsync_ShouldTransitionFromInProgressToCompleted()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.MarkInProgressAsync(item.Id);

        await _service.MarkCompletedAsync(item.Id);

        var updated = await _queueRepository.GetByIdAsync(item.Id);
        updated!.Status.Should().Be(QueueStatus.Completed);
    }

    [Fact]
    public async Task MarkCompletedAsync_ShouldThrowIfNotInProgress()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);

        var act = () => _service.MarkCompletedAsync(item.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not in InProgress status*");
    }

    [Fact]
    public async Task MarkCompletedAsync_ShouldUpdateTimestamp()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.MarkInProgressAsync(item.Id);
        var beforeUpdate = (await _queueRepository.GetByIdAsync(item.Id))!.UpdatedAt;

        await Task.Delay(10);
        await _service.MarkCompletedAsync(item.Id);

        var updated = await _queueRepository.GetByIdAsync(item.Id);
        updated!.UpdatedAt.Should().BeAfter(beforeUpdate);
    }

    #endregion

    #region MarkFailedAsync Tests

    [Fact]
    public async Task MarkFailedAsync_ShouldTransitionToRetrying_WhenAttemptsUnderMax()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.MarkInProgressAsync(item.Id);

        await _service.MarkFailedAsync(item.Id, "Network error");

        var updated = await _queueRepository.GetByIdAsync(item.Id);
        updated!.Status.Should().Be(QueueStatus.Retrying);
        updated.Attempts.Should().Be(1);
        updated.LastError.Should().Be("Network error");
        updated.NextRetryAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkFailedAsync_ShouldTransitionToFailed_WhenMaxAttemptsReached()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.MarkInProgressAsync(item.Id);

        // Fail 5 times to reach max attempts
        for (int i = 0; i < 5; i++)
        {
            await _service.MarkFailedAsync(item.Id, $"Error {i + 1}");
            if (i < 4)
            {
                // Re-enqueue for retry to continue testing
                var queueItem = await _queueRepository.GetByIdAsync(item.Id);
                queueItem!.Status = QueueStatus.Pending;
                await _queueRepository.UpdateAsync(queueItem);
                await _service.MarkInProgressAsync(item.Id);
            }
        }

        var updated = await _queueRepository.GetByIdAsync(item.Id);
        updated!.Status.Should().Be(QueueStatus.Failed);
        updated.Attempts.Should().Be(5);
    }

    [Fact]
    public async Task MarkFailedAsync_ShouldScheduleExponentialBackoff()
    {
        var expectedDelays = new[]
        {
            TimeSpan.Zero,           // Attempt 1: Immediate
            TimeSpan.FromMinutes(5), // Attempt 2
            TimeSpan.FromMinutes(30), // Attempt 3
            TimeSpan.FromHours(2),   // Attempt 4
            TimeSpan.FromHours(8)    // Attempt 5
        };

        var item = await _service.EnqueueAsync(_testEpisode.Id);

        for (int attempt = 0; attempt < 5; attempt++)
        {
            await _service.MarkInProgressAsync(item.Id);
            await _service.MarkFailedAsync(item.Id, $"Error {attempt + 1}");

            var updated = await _queueRepository.GetByIdAsync(item.Id);

            if (updated!.Status == QueueStatus.Retrying)
            {
                updated.NextRetryAt.Should().NotBeNull();
                var delay = updated.NextRetryAt!.Value - DateTimeOffset.UtcNow;
                // Allow 1 minute tolerance for test execution time
                delay.Should().BeCloseTo(expectedDelays[attempt], TimeSpan.FromMinutes(1));
            }

            // Re-enqueue for next iteration
            if (attempt < 4)
            {
                updated.Status = QueueStatus.Pending;
                await _queueRepository.UpdateAsync(updated);
            }
        }
    }

    [Fact]
    public async Task MarkFailedAsync_ShouldThrowIfNotInProgress()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);

        var act = () => _service.MarkFailedAsync(item.Id, "Error");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not in InProgress status*");
    }

    [Fact]
    public async Task MarkFailedAsync_WithCustomRetryAt_ShouldUseProvidedTime()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.MarkInProgressAsync(item.Id);
        var customRetryAt = DateTimeOffset.UtcNow.AddHours(1);

        await _service.MarkFailedAsync(item.Id, "Custom error", customRetryAt);

        var updated = await _queueRepository.GetByIdAsync(item.Id);
        updated!.NextRetryAt.Should().BeCloseTo(customRetryAt, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region CancelAsync Tests

    [Fact]
    public async Task CancelAsync_ShouldTransitionFromPendingToCancelled()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);

        await _service.CancelAsync(item.Id);

        var updated = await _queueRepository.GetByIdAsync(item.Id);
        updated!.Status.Should().Be(QueueStatus.Cancelled);
    }

    [Fact]
    public async Task CancelAsync_ShouldTransitionFromRetryingToCancelled()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.MarkInProgressAsync(item.Id);
        await _service.MarkFailedAsync(item.Id, "Error");

        await _service.CancelAsync(item.Id);

        var updated = await _queueRepository.GetByIdAsync(item.Id);
        updated!.Status.Should().Be(QueueStatus.Cancelled);
    }

    [Fact]
    public async Task CancelAsync_ShouldNotCancelInProgressItem()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.MarkInProgressAsync(item.Id);

        var act = () => _service.CancelAsync(item.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be cancelled*");
    }

    [Fact]
    public async Task CancelAsync_ShouldNotCancelCompletedItem()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.MarkInProgressAsync(item.Id);
        await _service.MarkCompletedAsync(item.Id);

        var act = () => _service.CancelAsync(item.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be cancelled*");
    }

    [Fact]
    public async Task CancelAsync_ShouldNotCancelFailedItem()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.MarkInProgressAsync(item.Id);
        // Fail 5 times to reach Failed status
        for (int i = 0; i < 5; i++)
        {
            await _service.MarkFailedAsync(item.Id, $"Error {i + 1}");
            if (i < 4)
            {
                var queueItem = await _queueRepository.GetByIdAsync(item.Id);
                queueItem!.Status = QueueStatus.Pending;
                await _queueRepository.UpdateAsync(queueItem);
                await _service.MarkInProgressAsync(item.Id);
            }
        }

        var act = () => _service.CancelAsync(item.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be cancelled*");
    }

    [Fact]
    public async Task CancelAsync_ShouldThrowIfItemNotFound()
    {
        var act = () => _service.CancelAsync("nonexistent-id");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region GetRetryableAsync Tests

    [Fact]
    public async Task GetRetryableAsync_ShouldReturnItemsReadyForRetry()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.MarkInProgressAsync(item.Id);
        await _service.MarkFailedAsync(item.Id, "Error");

        // Set NextRetryAt to past
        var queueItem = await _queueRepository.GetByIdAsync(item.Id);
        queueItem!.NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await _queueRepository.UpdateAsync(queueItem);

        var result = await _service.GetRetryableAsync();

        result.Should().HaveCount(1);
        result.First().Id.Should().Be(item.Id);
    }

    [Fact]
    public async Task GetRetryableAsync_ShouldNotReturnFutureRetryItems()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        await _service.MarkInProgressAsync(item.Id);

        // Use a custom future retry time (2nd attempt would be 5min, but let's use 1 hour)
        await _service.MarkFailedAsync(item.Id, "Error", DateTimeOffset.UtcNow.AddHours(1));

        // NextRetryAt is in the future
        var result = await _service.GetRetryableAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRetryableAsync_ShouldNotReturnNonRetryingItems()
    {
        var item = await _service.EnqueueAsync(_testEpisode.Id);
        // Still Pending, not Retrying

        var result = await _service.GetRetryableAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRetryableAsync_ShouldOrderByPriorityAndNextRetryAt()
    {
        var episode2 = CreateEpisode("retry-ep-2", "retryvid2");
        var episode3 = CreateEpisode("retry-ep-3", "retryvid3");
        var episode4 = CreateEpisode("retry-ep-4", "retryvid4");

        var items = new[]
        {
            await _service.EnqueueAsync(_testEpisode.Id, priority: 5),
            await _service.EnqueueAsync(episode2.Id, priority: 1),
            await _service.EnqueueAsync(episode3.Id, priority: 5),
            await _service.EnqueueAsync(episode4.Id, priority: 3)
        };

        // Set all to retrying with past retry times
        foreach (var item in items)
        {
            await _service.MarkInProgressAsync(item.Id);
            await _service.MarkFailedAsync(item.Id, "Error");
            var queueItem = await _queueRepository.GetByIdAsync(item.Id);
            queueItem!.NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await _queueRepository.UpdateAsync(queueItem);
        }

        var result = await _service.GetRetryableAsync();

        result.Should().HaveCount(4);
        result.First().Priority.Should().Be(1); // Highest priority first
    }

    #endregion

    #region Helper Methods

    private Episode CreateEpisode(string id, string videoId)
    {
        var episode = new Episode
        {
            Id = id,
            VideoId = videoId,
            ChannelId = _testChannel.Id,
            Title = $"Episode {id}",
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