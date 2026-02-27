using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Xunit;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;

namespace Yallarhorn.Tests.Unit.Data.Entities;

/// <summary>
/// Unit tests for the Channel entity.
/// </summary>
public class ChannelTests
{
    [Fact]
    public void Channel_ShouldHaveCorrectTableName()
    {
        // Arrange & Act
        var attributes = typeof(Channel)
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.Schema.TableAttribute), false);

        // Assert
        attributes.Should().HaveCount(1);
        var tableAttr = (System.ComponentModel.DataAnnotations.Schema.TableAttribute)attributes[0];
        tableAttr.Name.Should().Be("channels");
    }

    [Fact]
    public void Channel_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var now = DateTimeOffset.UtcNow;
        var channel = new Channel
        {
            Id = "test-channel-1",
            Url = "https://youtube.com/@test",
            Title = "Test Channel",
            CreatedAt = now,
            UpdatedAt = now
        };

        // Assert
        channel.Id.Should().Be("test-channel-1");
        channel.Url.Should().Be("https://youtube.com/@test");
        channel.Title.Should().Be("Test Channel");
        channel.CreatedAt.Should().Be(now);
        channel.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void Channel_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var channel = new Channel
        {
            Id = "test",
            Url = "https://youtube.com/@test",
            Title = "Test",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        channel.EpisodeCountConfig.Should().Be(50);
        channel.FeedType.Should().Be(FeedType.Audio);
        channel.Enabled.Should().BeTrue();
        channel.Description.Should().BeNull();
        channel.ThumbnailUrl.Should().BeNull();
        channel.LastRefreshAt.Should().BeNull();
        channel.Episodes.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Channel_ShouldAllowSettingNullableProperties()
    {
        // Arrange
        var lastRefresh = DateTimeOffset.UtcNow.AddMinutes(-30);
        var channel = new Channel
        {
            Id = "test",
            Url = "https://youtube.com/@test",
            Title = "Test",
            Description = "A test channel",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            EpisodeCountConfig = 100,
            FeedType = FeedType.Both,
            Enabled = false,
            LastRefreshAt = lastRefresh,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        channel.Description.Should().Be("A test channel");
        channel.ThumbnailUrl.Should().Be("https://example.com/thumb.jpg");
        channel.EpisodeCountConfig.Should().Be(100);
        channel.FeedType.Should().Be(FeedType.Both);
        channel.Enabled.Should().BeFalse();
        channel.LastRefreshAt.Should().Be(lastRefresh);
    }

    [Fact]
    public void Channel_ShouldHaveRequiredAttributeOnRequiredProperties()
    {
        // Arrange
        var urlProperty = typeof(Channel).GetProperty(nameof(Channel.Url));
        var titleProperty = typeof(Channel).GetProperty(nameof(Channel.Title));
        var createdAtProperty = typeof(Channel).GetProperty(nameof(Channel.CreatedAt));
        var updatedAtProperty = typeof(Channel).GetProperty(nameof(Channel.UpdatedAt));

        // Act
        var urlRequired = urlProperty?.GetCustomAttributes(typeof(RequiredAttribute), false);
        var titleRequired = titleProperty?.GetCustomAttributes(typeof(RequiredAttribute), false);
        var createdAtRequired = createdAtProperty?.GetCustomAttributes(typeof(RequiredAttribute), false);
        var updatedAtRequired = updatedAtProperty?.GetCustomAttributes(typeof(RequiredAttribute), false);

        // Assert
        // Note: Id has [Key] which implies required in EF Core
        urlRequired.Should().HaveCount(1);
        titleRequired.Should().HaveCount(1);
        createdAtRequired.Should().HaveCount(1);
        updatedAtRequired.Should().HaveCount(1);
    }

    [Fact]
    public void Channel_ShouldHaveKeyAttributeOnId()
    {
        // Arrange
        var idProperty = typeof(Channel).GetProperty(nameof(Channel.Id));

        // Act
        var keyAttr = idProperty?.GetCustomAttributes(typeof(KeyAttribute), false);

        // Assert
        keyAttr.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(FeedType.Audio)]
    [InlineData(FeedType.Video)]
    [InlineData(FeedType.Both)]
    public void Channel_ShouldSupportAllFeedTypes(FeedType feedType)
    {
        // Arrange & Act
        var channel = new Channel
        {
            Id = "test",
            Url = "https://youtube.com/@test",
            Title = "Test",
            FeedType = feedType,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        channel.FeedType.Should().Be(feedType);
    }
}