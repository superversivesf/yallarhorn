using FluentAssertions;
using Xunit;
using Yallarhorn.Data.Enums;

namespace Yallarhorn.Tests.Unit.Data.Enums;

/// <summary>
/// Unit tests for the FeedType enum.
/// </summary>
public class FeedTypeTests
{
    [Fact]
    public void FeedType_ShouldHaveThreeValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<FeedType>();

        // Assert
        values.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(FeedType.Audio, 0)]
    [InlineData(FeedType.Video, 1)]
    [InlineData(FeedType.Both, 2)]
    public void FeedType_ShouldHaveExpectedValues(FeedType feedType, int expectedValue)
    {
        // Assert
        ((int)feedType).Should().Be(expectedValue);
    }

    [Fact]
    public void FeedType_ShouldParseFromString()
    {
        // Arrange & Act
        var audio = Enum.Parse<FeedType>("Audio");
        var video = Enum.Parse<FeedType>("Video");
        var both = Enum.Parse<FeedType>("Both");

        // Assert
        audio.Should().Be(FeedType.Audio);
        video.Should().Be(FeedType.Video);
        both.Should().Be(FeedType.Both);
    }

    [Fact]
    public void FeedType_ShouldHaveValidNames()
    {
        // Assert
        Enum.GetNames<FeedType>().Should().Contain(["Audio", "Video", "Both"]);
    }
}