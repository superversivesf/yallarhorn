namespace Yallarhorn.Tests.Unit.Logging;

using FluentAssertions;
using Serilog.Events;
using Xunit;
using Yallarhorn.Logging;

public class LoggingOptionsTests
{
    [Fact]
    public void Constructor_ShouldSetDefaultValues()
    {
        // Arrange & Act
        var options = new LoggingOptions();

        // Assert
        options.MinimumLevel.Should().Be("Information");
        options.EnableConsole.Should().BeTrue();
        options.EnableFile.Should().BeTrue();
        options.FilePath.Should().Be("logs/yallarhorn-.log");
        options.RollingInterval.Should().Be("Day");
        options.RetainedFileCount.Should().Be(31);
    }

    [Theory]
    [InlineData("Verbose", LogEventLevel.Verbose)]
    [InlineData("verbose", LogEventLevel.Verbose)]
    [InlineData("Debug", LogEventLevel.Debug)]
    [InlineData("debug", LogEventLevel.Debug)]
    [InlineData("Information", LogEventLevel.Information)]
    [InlineData("information", LogEventLevel.Information)]
    [InlineData("Info", LogEventLevel.Information)]
    [InlineData("info", LogEventLevel.Information)]
    [InlineData("Warning", LogEventLevel.Warning)]
    [InlineData("warning", LogEventLevel.Warning)]
    [InlineData("Warn", LogEventLevel.Warning)]
    [InlineData("warn", LogEventLevel.Warning)]
    [InlineData("Error", LogEventLevel.Error)]
    [InlineData("error", LogEventLevel.Error)]
    [InlineData("Fatal", LogEventLevel.Fatal)]
    [InlineData("fatal", LogEventLevel.Fatal)]
    public void GetMinimumLogEventLevel_ShouldReturnCorrectLevel(string input, LogEventLevel expected)
    {
        // Arrange
        var options = new LoggingOptions { MinimumLevel = input };

        // Act
        var result = options.GetMinimumLogEventLevel();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Minute", Serilog.RollingInterval.Minute)]
    [InlineData("minute", Serilog.RollingInterval.Minute)]
    [InlineData("Hour", Serilog.RollingInterval.Hour)]
    [InlineData("hour", Serilog.RollingInterval.Hour)]
    [InlineData("Day", Serilog.RollingInterval.Day)]
    [InlineData("day", Serilog.RollingInterval.Day)]
    [InlineData("Month", Serilog.RollingInterval.Month)]
    [InlineData("month", Serilog.RollingInterval.Month)]
    [InlineData("Year", Serilog.RollingInterval.Year)]
    [InlineData("year", Serilog.RollingInterval.Year)]
    [InlineData("Infinite", Serilog.RollingInterval.Infinite)]
    [InlineData("infinite", Serilog.RollingInterval.Infinite)]
    public void GetSerilogRollingInterval_ShouldReturnCorrectInterval(string input, Serilog.RollingInterval expected)
    {
        // Arrange
        var options = new LoggingOptions { RollingInterval = input };

        // Act
        var result = options.GetSerilogRollingInterval();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("InvalidLevel")]
    [InlineData("unknown")]
    public void GetMinimumLogEventLevel_WithInvalidLevel_ShouldReturnInformation(string? invalidLevel)
    {
        // Arrange
        var options = new LoggingOptions { MinimumLevel = invalidLevel! };

        // Act
        var result = options.GetMinimumLogEventLevel();

        // Assert
        result.Should().Be(LogEventLevel.Information);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Invalid")]
    public void GetSerilogRollingInterval_WithInvalidInterval_ShouldReturnDay(string? invalidInterval)
    {
        // Arrange
        var options = new LoggingOptions { RollingInterval = invalidInterval! };

        // Act
        var result = options.GetSerilogRollingInterval();

        // Assert
        result.Should().Be(Serilog.RollingInterval.Day);
    }

    [Fact]
    public void Validate_WithValidOptions_ShouldNotThrow()
    {
        // Arrange
        var options = new LoggingOptions();

        // Act & Assert
        options.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("InvalidLevel")]
    [InlineData("Unknown")]
    public void Validate_WithInvalidMinimumLevel_ShouldThrow(string? invalidLevel)
    {
        // Arrange
        var options = new LoggingOptions { MinimumLevel = invalidLevel! };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage($"Invalid minimum log level: {invalidLevel}*");
    }

    [Fact]
    public void Validate_WithZeroRetainedFileCount_ShouldThrow()
    {
        // Arrange
        var options = new LoggingOptions { RetainedFileCount = 0 };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*RetainedFileCount must be at least 1*");
    }

    [Fact]
    public void Validate_WithNegativeRetainedFileCount_ShouldThrow()
    {
        // Arrange
        var options = new LoggingOptions { RetainedFileCount = -1 };

        // Act & Assert
        options.Invoking(o => o.Validate())
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*RetainedFileCount must be at least 1*");
    }

    [Fact]
    public void Validate_WithNullRetainedFileCount_ShouldNotThrow()
    {
        // Arrange
        var options = new LoggingOptions { RetainedFileCount = null };

        // Act & Assert
        options.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Fact]
    public void SectionName_ShouldBeCorrect()
    {
        // Assert
        LoggingOptions.SectionName.Should().Be("LoggingOptions");
    }
}