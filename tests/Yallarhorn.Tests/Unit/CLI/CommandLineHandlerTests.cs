namespace Yallarhorn.Tests.Unit.CLI;

using FluentAssertions;
using Xunit;
using Yallarhorn.CLI;
using Microsoft.Extensions.Logging;
using Moq;
using Yallarhorn.CLI.Commands;

public class CommandLineHandlerTests
{
    private readonly Mock<ILogger<CommandLineHandler>> _loggerMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly CommandLineHandler _handler;

    public CommandLineHandlerTests()
    {
        _loggerMock = new Mock<ILogger<CommandLineHandler>>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _handler = new CommandLineHandler(_loggerMock.Object, _serviceProviderMock.Object);
    }

    [Fact]
    public void CommandLineHandler_ShouldParseConfigFlag()
    {
        // Arrange
        var args = new[] { "--config", "/custom/path/config.yaml" };

        // Act
        var options = _handler.Parse(args);

        // Assert
        options.Should().NotBeNull();
        options!.ConfigPath.Should().Be("/custom/path/config.yaml");
    }

    [Fact]
    public void CommandLineHandler_ShouldParseShortConfigFlag()
    {
        // Arrange
        var args = new[] { "-c", "/custom/path/config.yaml" };

        // Act
        var options = _handler.Parse(args);

        // Assert
        options.Should().NotBeNull();
        options!.ConfigPath.Should().Be("/custom/path/config.yaml");
    }

    [Fact]
    public void CommandLineHandler_ShouldParseConfigValidateCommand()
    {
        // Arrange
        var args = new[] { "config", "validate" };

        // Act
        var options = _handler.Parse(args);

        // Assert
        options.Should().NotBeNull();
        options!.Command.Should().Be("config");
        options.SubCommand.Should().Be("validate");
    }

    [Fact]
    public void CommandLineHandler_ShouldParseConfigLintCommand()
    {
        // Arrange
        var args = new[] { "config", "lint" };

        // Act
        var options = _handler.Parse(args);

        // Assert
        options.Should().NotBeNull();
        options!.Command.Should().Be("config");
        options.SubCommand.Should().Be("lint");
    }

    [Fact]
    public void CommandLineHandler_ShouldParseAuthHashPasswordCommand()
    {
        // Arrange
        var args = new[] { "auth", "hash-password", "--password", "secret" };

        // Act
        var options = _handler.Parse(args);

        // Assert
        options.Should().NotBeNull();
        options!.Command.Should().Be("auth");
        options.SubCommand.Should().Be("hash-password");
        options.RemainingArgs.Should().Contain("--password");
        options.RemainingArgs.Should().Contain("secret");
    }

    [Fact]
    public void CommandLineHandler_ShouldReturnNull_WhenNoArgs()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        var options = _handler.Parse(args);

        // Assert
        options.Should().BeNull();
    }

    [Fact]
    public void CommandLineHandler_ShouldReturnNull_ForRunCommand()
    {
        // Arrange
        var args = new[] { "run" };

        // Act
        var options = _handler.Parse(args);

        // Assert
        options.Should().BeNull(); // null means "run the server"
    }

    [Fact]
    public void CommandLineHandler_ShouldParseConfigWithCustomPath()
    {
        // Arrange
        var args = new[] { "--config", "/etc/yallarhorn/config.yaml", "config", "validate" };

        // Act
        var options = _handler.Parse(args);

        // Assert
        options.Should().NotBeNull();
        options!.ConfigPath.Should().Be("/etc/yallarhorn/config.yaml");
        options.Command.Should().Be("config");
        options.SubCommand.Should().Be("validate");
    }

    [Fact]
    public void CommandLineHandler_ShouldParseHelpCommand()
    {
        // Arrange
        var args = new[] { "--help" };

        // Act
        var options = _handler.Parse(args);

        // Assert - help should be handled specially, returns options with HelpRequested = true
        options.Should().NotBeNull();
        options!.HelpRequested.Should().BeTrue();
    }

    [Fact]
    public void CommandLineHandler_ShouldParseVersionCommand()
    {
        // Arrange
        var args = new[] { "--version" };

        // Act
        var options = _handler.Parse(args);

        // Assert
        options.Should().NotBeNull();
        options!.VersionRequested.Should().BeTrue();
    }
}