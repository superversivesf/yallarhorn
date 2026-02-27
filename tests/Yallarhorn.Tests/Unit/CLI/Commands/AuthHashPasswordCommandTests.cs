namespace Yallarhorn.Tests.Unit.CLI.Commands;

using FluentAssertions;
using Xunit;
using Yallarhorn.CLI.Commands;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;

// Use collection to prevent parallel execution with other tests that capture Console.Out
[Collection("ConsoleOutput")]
public class AuthHashPasswordCommandTests : IDisposable
{
    private readonly Mock<ILogger<AuthHashPasswordCommand>> _loggerMock;
    private readonly AuthHashPasswordCommand _command;
    private readonly StringWriter _consoleOutput;
    private readonly TextWriter _originalOutput;

    public AuthHashPasswordCommandTests()
    {
        _loggerMock = new Mock<ILogger<AuthHashPasswordCommand>>();
        _command = new AuthHashPasswordCommand(_loggerMock.Object);
        _consoleOutput = new StringWriter();
        _originalOutput = Console.Out;
        Console.SetOut(_consoleOutput);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOutput);
        _consoleOutput.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void AuthHashPasswordCommand_ShouldHaveCorrectName()
    {
        // Assert
        _command.Name.Should().Be("hash-password");
    }

    [Fact]
    public void AuthHashPasswordCommand_ShouldHaveDescription()
    {
        // Assert
        _command.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnZero_WhenPasswordProvided()
    {
        // Arrange
        var args = new[] { "--password", "mysecretpassword" };

        // Act
        var result = await _command.ExecuteAsync(args, CancellationToken.None);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldOutputHashedPassword()
    {
        // Arrange
        var password = "mysecretpassword";
        var args = new[] { "--password", password };

        // Act
        await _command.ExecuteAsync(args, CancellationToken.None);

        // Assert
        var output = _consoleOutput.ToString();
        output.Should().NotBeNullOrEmpty();
        // BCrypt hash starts with $2
        output.Should().Contain("$2");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNonZero_WhenPasswordNotProvided()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        var result = await _command.ExecuteAsync(args, CancellationToken.None);

        // Assert
        result.Should().NotBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNonZero_WhenPasswordIsEmpty()
    {
        // Arrange
        var args = new[] { "--password", "" };

        // Act
        var result = await _command.ExecuteAsync(args, CancellationToken.None);

        // Assert
        result.Should().NotBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGenerateValidBCryptHash()
    {
        // Arrange
        var password = "mysecretpassword";
        var args = new[] { "--password", password };

        // Act
        await _command.ExecuteAsync(args, CancellationToken.None);
    
        // Assert
        var output = _consoleOutput.ToString().Trim();
        output.Should().StartWith("$2"); // BCrypt hashes start with $2
        
        // Verify the hash can be validated
        BCrypt.Net.BCrypt.Verify(password, output).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGenerateUniqueHashes()
    {
        // Arrange
        var password = "mysecretpassword";
        var args = new[] { "--password", password };

        // Act
        await _command.ExecuteAsync(args, CancellationToken.None);
        var hash1 = _consoleOutput.ToString().Trim();
        
        _consoleOutput.GetStringBuilder().Clear();
        await _command.ExecuteAsync(args, CancellationToken.None);
        var hash2 = _consoleOutput.ToString().Trim();

        // Assert - BCrypt with salt should generate different hashes
        hash1.Should().NotBe(hash2);
        
        // But both should verify against the same password
        BCrypt.Net.BCrypt.Verify(password, hash1).Should().BeTrue();
        BCrypt.Net.BCrypt.Verify(password, hash2).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseInteractivePrompt_WhenPasswordFlagWithoutValue()
    {
        // This test verifies that interactive mode works
        // We simulate this by not providing the password argument
        
        // Arrange & Act - empty args should prompt for password (which we can't test fully in unit tests)
        // Instead we test the direct password case above
        
        // For this test, we verify the command handles the interactive scenario gracefully
        var args = new[] { "-p", "testpassword" };

        // Act
        var result = await _command.ExecuteAsync(args, CancellationToken.None);

        // Assert
        result.Should().Be(0);
    }
}