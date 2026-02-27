using FluentAssertions;
using Xunit;
using Yallarhorn.Utilities;

namespace Yallarhorn.Tests.Unit.Utilities;

public class DurationFormatterTests
{
    [Fact]
    public void Format_ShouldFormatZeroSeconds()
    {
        // Act
        var result = DurationFormatter.Format(0);

        // Assert
        result.Should().Be("00:00:00");
    }

    [Fact]
    public void Format_ShouldFormatSecondsOnly()
    {
        // Act
        var result = DurationFormatter.Format(45);

        // Assert
        result.Should().Be("00:00:45");
    }

    [Fact]
    public void Format_ShouldFormatMinutes()
    {
        // Act
        var result = DurationFormatter.Format(90);

        // Assert
        result.Should().Be("00:01:30");
    }

    [Fact]
    public void Format_ShouldFormatHours()
    {
        // Act
        var result = DurationFormatter.Format(3725);

        // Assert
        result.Should().Be("01:02:05");
    }

    [Fact]
    public void Format_ShouldFormatLargeDuration()
    {
        // Arrange
        var seconds = 3661; // 1 hour, 1 minute, 1 second
        var expected = "01:01:01";

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Format_ShouldPaddingWithZero()
    {
        // Act
        var result = DurationFormatter.Format(5);

        // Assert
        result.Should().Be("00:00:05");
    }

    [Fact]
    public void Format_ShouldHandleExactMinute()
    {
        // Act
        var result = DurationFormatter.Format(60);

        // Assert
        result.Should().Be("00:01:00");
    }

    [Fact]
    public void Format_ShouldHandleExactHour()
    {
        // Act
        var result = DurationFormatter.Format(3600);

        // Assert
        result.Should().Be("01:00:00");
    }

    [Fact]
    public void Format_ShouldHandleMultipleHours()
    {
        // Arrange
        var seconds = 7329; // 2 hours, 2 minutes, 9 seconds
        var expected = "02:02:09";

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Format_ShouldHandleVeryLongDuration()
    {
        // Arrange
        var seconds = 100_000; // 27 hours, 46 minutes, 40 seconds
        var expected = "27:46:40";

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Format_ShouldThrowForNegativeSeconds()
    {
        // Act
        var act = () => DurationFormatter.Format(-1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("seconds");
    }

    [Fact]
    public void Format_ShouldHandleFiftyNineMinutesFiftyNineSeconds()
    {
        // Act
        var result = DurationFormatter.Format(3599);

        // Assert
        result.Should().Be("00:59:59");
    }

    [Fact]
    public void Format_ShouldHandleTypicalPodcastDuration()
    {
        // Arrange
        var seconds = 3425; // 57 minutes, 5 seconds
        var expected = "00:57:05";

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Format_ShouldHandleLongFormVideo()
    {
        // Arrange
        var seconds = 7265; // 2 hours, 1 minute, 5 seconds
        var expected = "02:01:05";

        // Act
        var result = DurationFormatter.Format(seconds);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void FormatWithMilliseconds_ShouldFormatWithMilliseconds()
    {
        // Act
        var result = DurationFormatter.FormatWithMilliseconds(90.5);

        // Assert
        result.Should().Be("00:01:30.500");
    }

    [Fact]
    public void FormatWithMilliseconds_ShouldFormatZeroWithMilliseconds()
    {
        // Act
        var result = DurationFormatter.FormatWithMilliseconds(0.0);

        // Assert
        result.Should().Be("00:00:00.000");
    }

    [Fact]
    public void FormatWithMilliseconds_ShouldHandleRoundedMilliseconds()
    {
        // Act
        var result = DurationFormatter.FormatWithMilliseconds(90.123);

        // Assert
        result.Should().Be("00:01:30.123");
    }
}