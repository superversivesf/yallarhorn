namespace Yallarhorn.Tests.Unit.Authentication;

using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Yallarhorn.Authentication;

public class BasicAuthHandlerTests
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly UrlEncoder _urlEncoder;

    public BasicAuthHandlerTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        _urlEncoder = UrlEncoder.Default;
    }

    #region Authentication Tests

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldSucceed_WithValidCredentials()
    {
        // Arrange
        var options = CreateOptions("testuser", "testpass", "Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        SetBasicAuthHeader(context, "testuser", "testpass");

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal!.Identity!.Name.Should().Be("testuser");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldFail_WithInvalidUsername()
    {
        // Arrange
        var options = CreateOptions("testuser", "testpass", "Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        SetBasicAuthHeader(context, "wronguser", "testpass");

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldFail_WithInvalidPassword()
    {
        // Arrange
        var options = CreateOptions("testuser", "testpass", "Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        SetBasicAuthHeader(context, "testuser", "wrongpass");

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldFail_WithMissingAuthorizationHeader()
    {
        // Arrange
        var options = CreateOptions("testuser", "testpass", "Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        // No Authorization header set

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldFail_WithInvalidBase64()
    {
        // Arrange
        var options = CreateOptions("testuser", "testpass", "Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        context.Request.Headers["Authorization"] = "Basic !!!InvalidBase64!!!";

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldFail_WithMalformedCredentials()
    {
        // Arrange - No colon separator
        var options = CreateOptions("testuser", "testpass", "Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("justusername"));
        context.Request.Headers["Authorization"] = $"Basic {credentials}";

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldFail_WithEmptyCredentials()
    {
        // Arrange
        var options = CreateOptions("testuser", "testpass", "Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(":"));
        context.Request.Headers["Authorization"] = $"Basic {credentials}";

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldFail_WithEmptyUsername()
    {
        // Arrange
        var options = CreateOptions("testuser", "testpass", "Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(":testpass"));
        context.Request.Headers["Authorization"] = $"Basic {credentials}";

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldFail_WithEmptyPassword()
    {
        // Arrange
        var options = CreateOptions("testuser", "testpass", "Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("testuser:"));
        context.Request.Headers["Authorization"] = $"Basic {credentials}";

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldFail_WithWrongScheme()
    {
        // Arrange
        var options = CreateOptions("testuser", "testpass", "Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("testuser:testpass"));
        context.Request.Headers["Authorization"] = $"Bearer {credentials}";

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    #endregion

    #region Challenge Response Tests

    [Fact]
    public async Task HandleChallengeAsync_ShouldSetWwwAuthenticateHeader()
    {
        // Arrange
        var options = CreateOptions("testuser", "testpass", "Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        await handler.ChallengeAsync(new AuthenticationProperties());

        // Assert
        context.Response.Headers.Should().ContainKey("WWW-Authenticate");
        var wwwAuth = context.Response.Headers["WWW-Authenticate"].ToString();
        wwwAuth.Should().Contain("Basic");
        wwwAuth.Should().Contain("realm=\"Test Realm\"");
    }

    [Fact]
    public async Task HandleChallengeAsync_ShouldUseCustomRealm()
    {
        // Arrange
        var options = CreateOptions("testuser", "testpass", "Custom Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        await handler.ChallengeAsync(new AuthenticationProperties());

        // Assert
        var wwwAuth = context.Response.Headers["WWW-Authenticate"].ToString();
        wwwAuth.Should().Contain("realm=\"Custom Test Realm\"");
    }

    #endregion

    #region Claims Tests

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldSetNameClaim()
    {
        // Arrange
        var options = CreateOptions("testuser", "testpass", "Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        SetBasicAuthHeader(context, "testuser", "testpass");

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst(ClaimTypes.Name)?.Value.Should().Be("testuser");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldSetNameIdentifierClaim()
    {
        // Arrange
        var options = CreateOptions("testuser", "testpass", "Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        SetBasicAuthHeader(context, "testuser", "testpass");

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be("testuser");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldUseSchemeNameAsAuthenticationType()
    {
        // Arrange
        var options = CreateOptions("testuser", "testpass", "Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        SetBasicAuthHeader(context, "testuser", "testpass");

        var scheme = CreateScheme();
        await handler.InitializeAsync(scheme, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal!.Identity!.AuthenticationType.Should().Be(scheme.Name);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldHandleSpecialCharactersInPassword()
    {
        // Arrange
        var options = CreateOptions("user", "p@ss:word!", "Test");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        SetBasicAuthHeader(context, "user", "p@ss:word!");

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldHandleColonInPassword()
    {
        // Arrange - password contains colon
        var options = CreateOptions("user", "pass:word", "Test");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        // Only first colon is separator
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("user:pass:word"));
        context.Request.Headers["Authorization"] = $"Basic {credentials}";

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ShouldFail_WhenNoCredentialsConfigured()
    {
        // Arrange
        var options = CreateOptions(null, null, "Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        SetBasicAuthHeader(context, "anyuser", "anypass");

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_IsCaseInsensitiveForScheme()
    {
        // Arrange
        var options = CreateOptions("testuser", "testpass", "Test Realm");
        var handler = CreateHandler(options);
        var context = CreateHttpContext();
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("testuser:testpass"));
        context.Request.Headers["Authorization"] = $"basic {credentials}"; // lowercase

        await handler.InitializeAsync(CreateScheme(), context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static IOptionsMonitor<BasicAuthOptions> CreateOptions(string? username, string? password, string realm)
    {
        var options = new BasicAuthOptions
        {
            Username = username,
            Password = password,
            Realm = realm
        };

        var mock = new Mock<IOptionsMonitor<BasicAuthOptions>>();
        mock.Setup(x => x.CurrentValue).Returns(options);
        mock.Setup(x => x.Get(It.IsAny<string>())).Returns(options);
        return mock.Object;
    }

    private BasicAuthHandler CreateHandler(IOptionsMonitor<BasicAuthOptions> options)
    {
        return new BasicAuthHandler(
            options,
            _loggerFactoryMock.Object,
            _urlEncoder);
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/";
        return context;
    }

    private static void SetBasicAuthHeader(HttpContext context, string username, string password)
    {
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
        context.Request.Headers["Authorization"] = $"Basic {credentials}";
    }

    private static AuthenticationScheme CreateScheme()
    {
        return new AuthenticationScheme(
            "Basic",
            "Basic Authentication",
            typeof(BasicAuthHandler));
    }

    #endregion
}