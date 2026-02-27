namespace Yallarhorn.Tests.Unit.CLI;

using FluentAssertions;
using Xunit;
using Yallarhorn.CLI;

public class CommandLineOptionsTests
{
    [Fact]
    public void CommandLineOptions_ShouldHaveConfigPathProperty()
    {
        // Arrange & Act
        var options = new CommandLineOptions
        {
            ConfigPath = "/custom/path/config.yaml"
        };

        // Assert
        options.ConfigPath.Should().Be("/custom/path/config.yaml");
    }

    [Fact]
    public void CommandLineOptions_ShouldDefaultConfigPathToNull()
    {
        // Arrange & Act
        var options = new CommandLineOptions();

        // Assert
        options.ConfigPath.Should().BeNull();
    }

    [Fact]
    public void CommandLineOptions_ShouldHaveCommandProperty()
    {
        // Arrange & Act
        var options = new CommandLineOptions
        {
            Command = "config"
        };

        // Assert
        options.Command.Should().Be("config");
    }

    [Fact]
    public void CommandLineOptions_ShouldDefaultCommandToNull()
    {
        // Arrange & Act
        var options = new CommandLineOptions();

        // Assert
        options.Command.Should().BeNull();
    }

    [Fact]
    public void CommandLineOptions_ShouldHaveSubCommandProperty()
    {
        // Arrange & Act
        var options = new CommandLineOptions
        {
            SubCommand = "validate"
        };

        // Assert
        options.SubCommand.Should().Be("validate");
    }

    [Fact]
    public void CommandLineOptions_ShouldHaveRemainingArgs()
    {
        // Arrange & Act
        var options = new CommandLineOptions
        {
            RemainingArgs = new[] { "--verbose", "--output", "json" }
        };

        // Assert
        options.RemainingArgs.Should().Contain("--verbose");
        options.RemainingArgs.Should().Contain("--output");
        options.RemainingArgs.Should().Contain("json");
    }
}