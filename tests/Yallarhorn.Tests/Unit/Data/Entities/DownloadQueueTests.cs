using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Xunit;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;

namespace Yallarhorn.Tests.Unit.Data.Entities;

/// <summary>
/// Unit tests for the DownloadQueue entity.
/// </summary>
public class DownloadQueueTests
{
    [Fact]
    public void DownloadQueue_ShouldHaveCorrectTableName()
    {
        // Arrange & Act
        var attributes = typeof(DownloadQueue)
            .GetCustomAttributes(typeof(TableAttribute), false);

        // Assert
        attributes.Should().HaveCount(1);
        var tableAttr = (TableAttribute)attributes[0];
        tableAttr.Name.Should().Be("download_queue");
    }

    [Fact]
    public void DownloadQueue_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var now = DateTimeOffset.UtcNow;
        var queueItem = new DownloadQueue
        {
            Id = "dq-001",
            EpisodeId = "ep-001",
            CreatedAt = now,
            UpdatedAt = now
        };

        // Assert
        queueItem.Id.Should().Be("dq-001");
        queueItem.EpisodeId.Should().Be("ep-001");
        queueItem.CreatedAt.Should().Be(now);
        queueItem.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void DownloadQueue_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var queueItem = new DownloadQueue
        {
            Id = "dq-001",
            EpisodeId = "ep-001",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        queueItem.Priority.Should().Be(5);
        queueItem.Status.Should().Be(QueueStatus.Pending);
        queueItem.Attempts.Should().Be(0);
        queueItem.MaxAttempts.Should().Be(3);
        queueItem.LastError.Should().BeNull();
        queueItem.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public void DownloadQueue_ShouldAllowSettingAllProperties()
    {
        // Arrange
        var nextRetry = DateTimeOffset.UtcNow.AddMinutes(30);

        // Act
        var queueItem = new DownloadQueue
        {
            Id = "dq-001",
            EpisodeId = "ep-001",
            Priority = 1,
            Status = QueueStatus.InProgress,
            Attempts = 2,
            MaxAttempts = 5,
            LastError = "Connection timeout",
            NextRetryAt = nextRetry,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        queueItem.Priority.Should().Be(1);
        queueItem.Status.Should().Be(QueueStatus.InProgress);
        queueItem.Attempts.Should().Be(2);
        queueItem.MaxAttempts.Should().Be(5);
        queueItem.LastError.Should().Be("Connection timeout");
        queueItem.NextRetryAt.Should().Be(nextRetry);
    }

    [Fact]
    public void DownloadQueue_ShouldHaveRequiredAttributeOnRequiredProperties()
    {
        // Arrange
        var episodeIdProperty = typeof(DownloadQueue).GetProperty(nameof(DownloadQueue.EpisodeId));
        var createdAtProperty = typeof(DownloadQueue).GetProperty(nameof(DownloadQueue.CreatedAt));
        var updatedAtProperty = typeof(DownloadQueue).GetProperty(nameof(DownloadQueue.UpdatedAt));

        // Act
        var episodeIdRequired = episodeIdProperty?.GetCustomAttributes(typeof(RequiredAttribute), false);
        var createdAtRequired = createdAtProperty?.GetCustomAttributes(typeof(RequiredAttribute), false);
        var updatedAtRequired = updatedAtProperty?.GetCustomAttributes(typeof(RequiredAttribute), false);

        // Assert
        // Note: Id has [Key] which implies required in EF Core
        episodeIdRequired.Should().HaveCount(1);
        createdAtRequired.Should().HaveCount(1);
        updatedAtRequired.Should().HaveCount(1);
    }

    [Fact]
    public void DownloadQueue_ShouldHaveKeyAttributeOnId()
    {
        // Arrange
        var idProperty = typeof(DownloadQueue).GetProperty(nameof(DownloadQueue.Id));

        // Act
        var keyAttr = idProperty?.GetCustomAttributes(typeof(KeyAttribute), false);

        // Assert
        keyAttr.Should().HaveCount(1);
    }

    [Fact]
    public void DownloadQueue_ShouldHaveRangeAttributeOnPriority()
    {
        // Arrange
        var priorityProperty = typeof(DownloadQueue).GetProperty(nameof(DownloadQueue.Priority));

        // Act
        var rangeAttr = priorityProperty?.GetCustomAttributes(typeof(RangeAttribute), false);

        // Assert
        rangeAttr.Should().HaveCount(1);
        var range = (RangeAttribute)rangeAttr![0];
        range.Minimum.Should().Be(1);
        range.Maximum.Should().Be(10);
    }

    [Fact]
    public void DownloadQueue_ShouldHaveForeignKeyAttributeOnEpisodeNavigation()
    {
        // Arrange
        var episodeProperty = typeof(DownloadQueue).GetProperty(nameof(DownloadQueue.Episode));

        // Act
        var foreignKeyAttr = episodeProperty?.GetCustomAttributes(typeof(ForeignKeyAttribute), false);

        // Assert
        foreignKeyAttr.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(QueueStatus.Pending)]
    [InlineData(QueueStatus.InProgress)]
    [InlineData(QueueStatus.Completed)]
    [InlineData(QueueStatus.Failed)]
    [InlineData(QueueStatus.Cancelled)]
    public void DownloadQueue_ShouldSupportAllStatuses(QueueStatus status)
    {
        // Arrange & Act
        var queueItem = new DownloadQueue
        {
            Id = "dq-001",
            EpisodeId = "ep-001",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        queueItem.Status.Should().Be(status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void DownloadQueue_ShouldSupportValidPriorities(int priority)
    {
        // Arrange & Act
        var queueItem = new DownloadQueue
        {
            Id = "dq-001",
            EpisodeId = "ep-001",
            Priority = priority,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        queueItem.Priority.Should().Be(priority);
    }

    [Fact]
    public void DownloadQueue_ShouldTrackRetryState()
    {
        // Arrange & Act
        var queueItem = new DownloadQueue
        {
            Id = "dq-001",
            EpisodeId = "ep-001",
            Status = QueueStatus.Failed,
            Attempts = 3,
            MaxAttempts = 3,
            LastError = "yt-dlp error: Video unavailable",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        queueItem.Attempts.Should().Be(queueItem.MaxAttempts);
        queueItem.Status.Should().Be(QueueStatus.Failed);
        queueItem.LastError.Should().Contain("unavailable");
    }
}