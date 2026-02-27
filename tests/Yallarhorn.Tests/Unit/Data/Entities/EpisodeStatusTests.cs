using FluentAssertions;
using Xunit;
using Yallarhorn.Data.Enums;

namespace Yallarhorn.Tests.Unit.Data.Enums;

/// <summary>
/// Unit tests for the EpisodeStatus enum.
/// </summary>
public class EpisodeStatusTests
{
    [Fact]
    public void EpisodeStatus_ShouldHaveSixValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<EpisodeStatus>();

        // Assert
        values.Should().HaveCount(6);
    }

    [Theory]
    [InlineData(EpisodeStatus.Pending, 0)]
    [InlineData(EpisodeStatus.Downloading, 1)]
    [InlineData(EpisodeStatus.Processing, 2)]
    [InlineData(EpisodeStatus.Completed, 3)]
    [InlineData(EpisodeStatus.Failed, 4)]
    [InlineData(EpisodeStatus.Deleted, 5)]
    public void EpisodeStatus_ShouldHaveExpectedValues(EpisodeStatus status, int expectedValue)
    {
        // Assert
        ((int)status).Should().Be(expectedValue);
    }

    [Fact]
    public void EpisodeStatus_ShouldParseFromString()
    {
        // Arrange & Act
        var pending = Enum.Parse<EpisodeStatus>("Pending");
        var downloading = Enum.Parse<EpisodeStatus>("Downloading");
        var processing = Enum.Parse<EpisodeStatus>("Processing");
        var completed = Enum.Parse<EpisodeStatus>("Completed");
        var failed = Enum.Parse<EpisodeStatus>("Failed");
        var deleted = Enum.Parse<EpisodeStatus>("Deleted");

        // Assert
        pending.Should().Be(EpisodeStatus.Pending);
        downloading.Should().Be(EpisodeStatus.Downloading);
        processing.Should().Be(EpisodeStatus.Processing);
        completed.Should().Be(EpisodeStatus.Completed);
        failed.Should().Be(EpisodeStatus.Failed);
        deleted.Should().Be(EpisodeStatus.Deleted);
    }

    [Fact]
    public void EpisodeStatus_ShouldHaveValidNames()
    {
        // Assert
        Enum.GetNames<EpisodeStatus>().Should().Contain(
        [
            "Pending", "Downloading", "Processing", "Completed", "Failed", "Deleted"
        ]);
    }
}