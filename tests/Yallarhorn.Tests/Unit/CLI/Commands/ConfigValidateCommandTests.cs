namespace Yallarhorn.Tests.Unit.CLI.Commands;

using FluentAssertions;
using Xunit;
using Yallarhorn.CLI.Commands;
using Microsoft.Extensions.Logging;
using Moq;
using Yallarhorn.Configuration;

// Use collection to prevent parallel execution with other tests that capture Console.Out
[Collection("ConsoleOutput")]
public class ConfigValidateCommandTests
{
    private readonly Mock<ILogger<ConfigValidateCommand>> _loggerMock;
    private readonly ConfigValidateCommand _command;

    public ConfigValidateCommandTests()
    {
        _loggerMock = new Mock<ILogger<ConfigValidateCommand>>();
        _command = new ConfigValidateCommand(_loggerMock.Object);
    }

    [Fact]
    public void ConfigValidateCommand_ShouldHaveCorrectName()
    {
        // Assert
        _command.Name.Should().Be("validate");
    }

    [Fact]
    public void ConfigValidateCommand_ShouldHaveDescription()
    {
        // Assert
        _command.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnZero_WhenConfigValid()
    {
        // Arrange
        var configPath = CreateValidConfigFile();

        try
        {
            // Act
            var result = await _command.ExecuteAsync(configPath, CancellationToken.None);

            // Assert
            result.Should().Be(0);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNonZero_WhenConfigInvalid()
    {
        // Arrange
        var configPath = CreateInvalidConfigFile();

        try
        {
            // Act
            var result = await _command.ExecuteAsync(configPath, CancellationToken.None);

            // Assert
            result.Should().NotBe(0);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNonZero_WhenConfigNotFound()
    {
        // Arrange - use a non-existent path
        var configPath = "/non/existent/path/config.yaml";

        // Act
        var result = await _command.ExecuteAsync(configPath, CancellationToken.None);

        // Assert
        result.Should().NotBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogValidationErrors_WhenConfigInvalid()
    {
        // Arrange
        var configPath = CreateInvalidConfigFile();

        try
        {
            // Act
            await _command.ExecuteAsync(configPath, CancellationToken.None);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("validation")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce());
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    private static string CreateValidConfigFile()
    {
        var tempPath = Path.GetTempFileName();
        var yaml = """
            version: "1.0"
            poll_interval: 3600
            max_concurrent_downloads: 3
            download_dir: "./downloads"
            temp_dir: "./temp"
            channels:
              - name: "Test Channel"
                url: "https://www.youtube.com/@testchannel"
                enabled: true
            server:
              port: 5001
            database:
              path: "./data/yallarhorn.db"
            """;
        File.WriteAllText(tempPath, yaml);
        return tempPath;
    }

    private static string CreateInvalidConfigFile()
    {
        var tempPath = Path.GetTempFileName();
        var yaml = """
            version: "1.0"
            poll_interval: 10
            max_concurrent_downloads: 50
            channels: []
            """;
        File.WriteAllText(tempPath, yaml);
        return tempPath;
    }
}