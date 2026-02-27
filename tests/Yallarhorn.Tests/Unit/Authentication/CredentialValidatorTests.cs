namespace Yallarhorn.Tests.Unit.Authentication;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yallarhorn.Authentication;

public class CredentialValidatorTests
{
    private readonly Mock<ILogger<CredentialValidator>> _loggerMock;
    private readonly ICredentialValidator _validator;

    public CredentialValidatorTests()
    {
        _loggerMock = new Mock<ILogger<CredentialValidator>>();
        _validator = new CredentialValidator(_loggerMock.Object);
    }

    #region BCrypt Validation Tests

    [Fact]
    public async Task ValidateAsync_ShouldValidateValidBCryptCredentials()
    {
        // Arrange
        var username = "testuser";
        var password = "testpassword";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            [username] = (hashedPassword, "feed")
        };

        // Act
        var result = await _validator.ValidateAsync(username, password, credentials);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectInvalidPassword_WithBCrypt()
    {
        // Arrange
        var username = "testuser";
        var correctPassword = "testpassword";
        var wrongPassword = "wrongpassword";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(correctPassword);
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            [username] = (hashedPassword, "feed")
        };

        // Act
        var result = await _validator.ValidateAsync(username, wrongPassword, credentials);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectUnknownUser_WithBCrypt()
    {
        // Arrange
        var username = "unknownuser";
        var password = "testpassword";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword("otherpassword");
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            ["otheruser"] = (hashedPassword, "feed")
        };

        // Act
        var result = await _validator.ValidateAsync(username, password, credentials);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Plaintext Validation Tests (Timing-Safe)

    [Fact]
    public async Task ValidateAsync_ShouldValidatePlaintextCredentials_WithTimingSafeComparison()
    {
        // Arrange
        var username = "testuser";
        var password = "plaintextpassword";
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            [username] = (password, "feed") // Plaintext, not hashed
        };

        // Act
        var result = await _validator.ValidateAsync(username, password, credentials);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectInvalidPlaintextPassword()
    {
        // Arrange
        var username = "testuser";
        var correctPassword = "plaintextpassword";
        var wrongPassword = "wrongpassword";
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            [username] = (correctPassword, "feed")
        };

        // Act
        var result = await _validator.ValidateAsync(username, wrongPassword, credentials);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ShouldAutoDetectPlaintext_WhenNotBCryptFormat()
    {
        // Arrange - plaintext that looks nothing like BCrypt hash
        var username = "testuser";
        var password = "env_var_password";
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            [username] = (password, "feed")
        };

        // Act
        var result = await _validator.ValidateAsync(username, password, credentials);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Domain-Aware Validation Tests

    [Fact]
    public async Task ValidateAsync_ShouldValidateCorrectDomain()
    {
        // Arrange
        var username = "feeduser";
        var password = "testpassword";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            [username] = (hashedPassword, "feed")
        };

        // Act
        var result = await _validator.ValidateAsync(username, password, credentials, "feed");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ShouldRejectWrongDomain()
    {
        // Arrange
        var username = "feeduser";
        var password = "testpassword";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            [username] = (hashedPassword, "feed")
        };

        // Act - trying to access "admin" domain with "feed" credentials
        var result = await _validator.ValidateAsync(username, password, credentials, "admin");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ShouldAllowAdminDomain()
    {
        // Arrange
        var username = "adminuser";
        var password = "adminpassword";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            [username] = (hashedPassword, "admin")
        };

        // Act
        var result = await _validator.ValidateAsync(username, password, credentials, "admin");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ShouldAllowAnonymous_WhenNoDomainSpecified()
    {
        // Arrange
        var username = "testuser";
        var password = "testpassword";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            [username] = (hashedPassword, "feed")
        };

        // Act - no domain check
        var result = await _validator.ValidateAsync(username, password, credentials);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task ValidateAsync_ShouldLogSuccessfulAuthentication()
    {
        // Arrange
        var username = "testuser";
        var password = "testpassword";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            [username] = (hashedPassword, "feed")
        };

        // Act
        await _validator.ValidateAsync(username, password, credentials);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully authenticated")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_ShouldLogFailedAuthentication_InvalidPassword()
    {
        // Arrange
        var username = "testuser";
        var correctPassword = "testpassword";
        var wrongPassword = "wrongpassword";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(correctPassword);
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            [username] = (hashedPassword, "feed")
        };

        // Act
        await _validator.ValidateAsync(username, wrongPassword, credentials);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed authentication")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_ShouldLogFailedAuthentication_UnknownUser()
    {
        // Arrange
        var username = "unknownuser";
        var password = "testpassword";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword("otherpassword");
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            ["otheruser"] = (hashedPassword, "feed")
        };

        // Act
        await _validator.ValidateAsync(username, password, credentials);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed authentication")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_ShouldLogFailedAuthentication_WrongDomain()
    {
        // Arrange
        var username = "feeduser";
        var password = "testpassword";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            [username] = (hashedPassword, "feed")
        };

        // Act
        await _validator.ValidateAsync(username, password, credentials, "admin");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Domain mismatch")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ValidateAsync_ShouldHandleEmptyCredentials()
    {
        // Arrange
        var credentials = new Dictionary<string, (string Hash, string Domain)>();

        // Act
        var result = await _validator.ValidateAsync("testuser", "testpass", credentials);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ShouldHandleNullUsername()
    {
        // Arrange
        var password = "testpassword";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            ["testuser"] = (hashedPassword, "feed")
        };

        // Act
        var result = await _validator.ValidateAsync(null!, password, credentials);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ShouldHandleNullPassword()
    {
        // Arrange
        var password = "testpassword";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            ["testuser"] = (hashedPassword, "feed")
        };

        // Act
        var result = await _validator.ValidateAsync("testuser", null!, credentials);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ShouldHandleEmptyUsername()
    {
        // Arrange
        var password = "testpassword";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            ["testuser"] = (hashedPassword, "feed")
        };

        // Act
        var result = await _validator.ValidateAsync("", password, credentials);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ShouldHandleEmptyPassword()
    {
        // Arrange
        var password = "testpassword";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        
        var credentials = new Dictionary<string, (string Hash, string Domain)>
        {
            ["testuser"] = (hashedPassword, "feed")
        };

        // Act
        var result = await _validator.ValidateAsync("testuser", "", credentials);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}