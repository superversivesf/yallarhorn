namespace Yallarhorn.Tests.Unit.CLI.Commands;

using FluentAssertions;
using Xunit;
using Yallarhorn.CLI.Commands;
using Microsoft.Extensions.Logging;
using Moq;

// Use collection to prevent parallel execution with other tests that capture Console.Out
[Collection("ConsoleOutput")]
public class ConfigLintCommandTests
{
    private readonly Mock<ILogger<ConfigLintCommand>> _loggerMock;
    private readonly ConfigLintCommand _command;

    public ConfigLintCommandTests()
    {
        _loggerMock = new Mock<ILogger<ConfigLintCommand>>();
        _command = new ConfigLintCommand(_loggerMock.Object);
    }

    [Fact]
    public void ConfigLintCommand_ShouldHaveCorrectName()
    {
        // Assert
        _command.Name.Should().Be("lint");
    }

    [Fact]
    public void ConfigLintCommand_ShouldHaveDescription()
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
    public async Task ExecuteAsync_ShouldReportWarnings_ForRecommendedSettings()
    {
        // Arrange
        var configPath = CreateConfigWithWarnings();

        try
        {
            // Act
            var result = await _command.ExecuteAsync(configPath, CancellationToken.None);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce());
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCheckForCommonIssues()
    {
        // Arrange
        var configPath = CreateValidConfigFile();

        try
        {
            // Act
            await _command.ExecuteAsync(configPath, CancellationToken.None);

            // Assert - should log info messages about checks performed
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce());
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNonZero_WhenConfigNotFound()
    {
        // Arrange
        var configPath = "/non/existent/path/config.yaml";

        // Act
        var result = await _command.ExecuteAsync(configPath, CancellationToken.None);

        // Assert
        result.Should().NotBe(0);
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
            auth:
              feed_credentials:
                enabled: false
              admin_auth:
                enabled: false
            """;
        File.WriteAllText(tempPath, yaml);
        return tempPath;
    }

    private static string CreateConfigWithWarnings()
    {
        var tempPath = Path.GetTempFileName();
        var yaml = """
            version: "1.0"
            poll_interval: 300
            max_concurrent_downloads: 1
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
            auth:
              feed_credentials:
                enabled: true
                username: "admin"
                password: "plaintext-password"
              admin_auth:
                enabled: true
                username: "admin"
                password: "plaintext-password"
            """;
        File.WriteAllText(tempPath, yaml);
        return tempPath;
    }
}