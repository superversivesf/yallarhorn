using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Xunit;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;

namespace Yallarhorn.Tests.Unit.Data.Entities;

/// <summary>
/// Unit tests for the Episode entity.
/// </summary>
public class EpisodeTests
{
    [Fact]
    public void Episode_ShouldHaveCorrectTableName()
    {
        // Arrange & Act
        var attributes = typeof(Episode)
            .GetCustomAttributes(typeof(TableAttribute), false);

        // Assert
        attributes.Should().HaveCount(1);
        var tableAttr = (TableAttribute)attributes[0];
        tableAttr.Name.Should().Be("episodes");
    }

    [Fact]
    public void Episode_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var now = DateTimeOffset.UtcNow;
        var episode = new Episode
        {
            Id = "ep-001",
            VideoId = "abc123",
            ChannelId = "channel-001",
            Title = "Test Episode",
            CreatedAt = now,
            UpdatedAt = now
        };

        // Assert
        episode.Id.Should().Be("ep-001");
        episode.VideoId.Should().Be("abc123");
        episode.ChannelId.Should().Be("channel-001");
        episode.Title.Should().Be("Test Episode");
        episode.CreatedAt.Should().Be(now);
        episode.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void Episode_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var episode = new Episode
        {
            Id = "ep-001",
            VideoId = "abc123",
            ChannelId = "channel-001",
            Title = "Test",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        episode.Status.Should().Be(EpisodeStatus.Pending);
        episode.RetryCount.Should().Be(0);
        episode.Description.Should().BeNull();
        episode.ThumbnailUrl.Should().BeNull();
        episode.DurationSeconds.Should().BeNull();
        episode.PublishedAt.Should().BeNull();
        episode.DownloadedAt.Should().BeNull();
        episode.FilePathAudio.Should().BeNull();
        episode.FilePathVideo.Should().BeNull();
        episode.FileSizeAudio.Should().BeNull();
        episode.FileSizeVideo.Should().BeNull();
        episode.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Episode_ShouldAllowSettingAllProperties()
    {
        // Arrange
        var published = DateTimeOffset.UtcNow.AddDays(-7);
        var downloaded = DateTimeOffset.UtcNow.AddHours(-1);

        // Act
        var episode = new Episode
        {
            Id = "ep-001",
            VideoId = "abc123",
            ChannelId = "channel-001",
            Title = "Test Episode",
            Description = "A test episode description",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            DurationSeconds = 3600,
            PublishedAt = published,
            DownloadedAt = downloaded,
            FilePathAudio = "channel/audio/abc123.mp3",
            FilePathVideo = "channel/video/abc123.mp4",
            FileSizeAudio = 5_000_000,
            FileSizeVideo = 50_000_000,
            Status = EpisodeStatus.Completed,
            RetryCount = 0,
            ErrorMessage = null,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-8),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        episode.Description.Should().Be("A test episode description");
        episode.ThumbnailUrl.Should().Be("https://example.com/thumb.jpg");
        episode.DurationSeconds.Should().Be(3600);
        episode.PublishedAt.Should().Be(published);
        episode.DownloadedAt.Should().Be(downloaded);
        episode.FilePathAudio.Should().Be("channel/audio/abc123.mp3");
        episode.FilePathVideo.Should().Be("channel/video/abc123.mp4");
        episode.FileSizeAudio.Should().Be(5_000_000);
        episode.FileSizeVideo.Should().Be(50_000_000);
        episode.Status.Should().Be(EpisodeStatus.Completed);
    }

    [Fact]
    public void Episode_ShouldHaveRequiredAttributeOnRequiredProperties()
    {
        // Arrange
        var videoIdProperty = typeof(Episode).GetProperty(nameof(Episode.VideoId));
        var channelIdProperty = typeof(Episode).GetProperty(nameof(Episode.ChannelId));
        var titleProperty = typeof(Episode).GetProperty(nameof(Episode.Title));
        var createdAtProperty = typeof(Episode).GetProperty(nameof(Episode.CreatedAt));
        var updatedAtProperty = typeof(Episode).GetProperty(nameof(Episode.UpdatedAt));

        // Act
        var videoIdRequired = videoIdProperty?.GetCustomAttributes(typeof(RequiredAttribute), false);
        var channelIdRequired = channelIdProperty?.GetCustomAttributes(typeof(RequiredAttribute), false);
        var titleRequired = titleProperty?.GetCustomAttributes(typeof(RequiredAttribute), false);
        var createdAtRequired = createdAtProperty?.GetCustomAttributes(typeof(RequiredAttribute), false);
        var updatedAtRequired = updatedAtProperty?.GetCustomAttributes(typeof(RequiredAttribute), false);

        // Assert
        // Note: Id has [Key] which implies required in EF Core
        videoIdRequired.Should().HaveCount(1);
        channelIdRequired.Should().HaveCount(1);
        titleRequired.Should().HaveCount(1);
        createdAtRequired.Should().HaveCount(1);
        updatedAtRequired.Should().HaveCount(1);
    }

    [Fact]
    public void Episode_ShouldHaveKeyAttributeOnId()
    {
        // Arrange
        var idProperty = typeof(Episode).GetProperty(nameof(Episode.Id));

        // Act
        var keyAttr = idProperty?.GetCustomAttributes(typeof(KeyAttribute), false);

        // Assert
        keyAttr.Should().HaveCount(1);
    }

    [Fact]
    public void Episode_ShouldHaveForeignKeyAttributeOnChannelNavigation()
    {
        // Arrange
        var channelProperty = typeof(Episode).GetProperty(nameof(Episode.Channel));

        // Act
        var foreignKeyAttr = channelProperty?.GetCustomAttributes(typeof(ForeignKeyAttribute), false);

        // Assert
        foreignKeyAttr.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(EpisodeStatus.Pending)]
    [InlineData(EpisodeStatus.Downloading)]
    [InlineData(EpisodeStatus.Processing)]
    [InlineData(EpisodeStatus.Completed)]
    [InlineData(EpisodeStatus.Failed)]
    [InlineData(EpisodeStatus.Deleted)]
    public void Episode_ShouldSupportAllStatuses(EpisodeStatus status)
    {
        // Arrange & Act
        var episode = new Episode
        {
            Id = "ep-001",
            VideoId = "abc123",
            ChannelId = "channel-001",
            Title = "Test",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        episode.Status.Should().Be(status);
    }

    [Fact]
    public void Episode_ShouldTrackRetryCount()
    {
        // Arrange
        var episode = new Episode
        {
            Id = "ep-001",
            VideoId = "abc123",
            ChannelId = "channel-001",
            Title = "Test",
            Status = EpisodeStatus.Failed,
            RetryCount = 3,
            ErrorMessage = "Download failed after 3 attempts",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        episode.RetryCount.Should().Be(3);
        episode.ErrorMessage.Should().Be("Download failed after 3 attempts");
    }
}