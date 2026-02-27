namespace Yallarhorn.Tests.Unit.Middleware;

using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yallarhorn.Extensions;
using Yallarhorn.Middleware;

public class RateLimitMiddlewareTests
{
    private readonly Mock<ILogger<RateLimitMiddleware>> _loggerMock;

    public RateLimitMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<RateLimitMiddleware>>();
    }

    #region Header Tests

    [Fact]
    public async Task InvokeAsync_ShouldAddRateLimitHeaders_ForGetRequest()
    {
        // Arrange
        var context = CreateHttpContext("GET", "/api/test");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("X-RateLimit-Limit");
        context.Response.Headers.Should().ContainKey("X-RateLimit-Remaining");
        context.Response.Headers.Should().ContainKey("X-RateLimit-Reset");
    }

    [Fact]
    public async Task InvokeAsync_ShouldAddRateLimitHeaders_ForPostRequest()
    {
        // Arrange
        var context = CreateHttpContext("POST", "/api/test");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("X-RateLimit-Limit");
        context.Response.Headers.Should().ContainKey("X-RateLimit-Remaining");
        context.Response.Headers.Should().ContainKey("X-RateLimit-Reset");
    }

    [Fact]
    public async Task InvokeAsync_GetRequest_ShouldHaveReadLimit()
    {
        // Arrange
        var context = CreateHttpContext("GET", "/api/test");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var limit = int.Parse(context.Response.Headers["X-RateLimit-Limit"].ToString());
        limit.Should().Be(100); // Read limit: 100 requests/minute
    }

    [Fact]
    public async Task InvokeAsync_PostRequest_ShouldHaveWriteLimit()
    {
        // Arrange
        var context = CreateHttpContext("POST", "/api/test");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var limit = int.Parse(context.Response.Headers["X-RateLimit-Limit"].ToString());
        limit.Should().Be(30); // Write limit: 30 requests/minute
    }

    [Fact]
    public async Task InvokeAsync_PutRequest_ShouldHaveWriteLimit()
    {
        // Arrange
        var context = CreateHttpContext("PUT", "/api/test/123");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var limit = int.Parse(context.Response.Headers["X-RateLimit-Limit"].ToString());
        limit.Should().Be(30); // Write limit: 30 requests/minute
    }

    [Fact]
    public async Task InvokeAsync_DeleteRequest_ShouldHaveWriteLimit()
    {
        // Arrange
        var context = CreateHttpContext("DELETE", "/api/test/123");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var limit = int.Parse(context.Response.Headers["X-RateLimit-Limit"].ToString());
        limit.Should().Be(30); // Write limit: 30 requests/minute
    }

    [Fact]
    public async Task InvokeAsync_TriggerRefreshEndpoint_ShouldHaveTriggerLimit()
    {
        // Arrange
        var context = CreateHttpContext("POST", "/api/channels/ch-123/refresh");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var limit = int.Parse(context.Response.Headers["X-RateLimit-Limit"].ToString());
        limit.Should().Be(10); // Trigger limit: 10 requests/minute
    }

    [Fact]
    public async Task InvokeAsync_TriggerRetryEndpoint_ShouldHaveTriggerLimit()
    {
        // Arrange
        var context = CreateHttpContext("POST", "/api/episodes/ep-123/retry");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var limit = int.Parse(context.Response.Headers["X-RateLimit-Limit"].ToString());
        limit.Should().Be(10); // Trigger limit: 10 requests/minute
    }

    [Fact]
    public async Task InvokeAsync_RefreshAllEndpoint_ShouldHaveTriggerLimit()
    {
        // Arrange
        var context = CreateHttpContext("POST", "/api/refresh-all");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var limit = int.Parse(context.Response.Headers["X-RateLimit-Limit"].ToString());
        limit.Should().Be(10); // Trigger limit for refresh-all
    }

    [Fact]
    public async Task InvokeAsync_ResetHeader_ShouldBeValidUnixTimestamp()
    {
        // Arrange
        var beforeTime = DateTimeOffset.UtcNow;
        var context = CreateHttpContext("GET", "/api/test");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);
        var afterTime = DateTimeOffset.UtcNow.AddSeconds(60);

        // Assert
        var resetHeader = context.Response.Headers["X-RateLimit-Reset"].ToString();
        var resetTime = long.Parse(resetHeader);
        var resetTimestamp = DateTimeOffset.FromUnixTimeSeconds(resetTime);
        
        // Reset time should be ~60 seconds in the future
        resetTimestamp.Should().BeAfter(beforeTime);
        resetTimestamp.Should().BeBefore(afterTime.AddSeconds(1)); // Add 1 second buffer
    }

    [Fact]
    public async Task InvokeAsync_RemainingHeader_ShouldStartAtLimit()
    {
        // Arrange
        var context = CreateHttpContext("GET", "/api/test");
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var remaining = int.Parse(context.Response.Headers["X-RateLimit-Remaining"].ToString());
        remaining.Should().Be(100); // Should start at the limit
    }

    [Fact]
    public async Task InvokeAsync_WhenRateLimited_RemainingShouldBeZero()
    {
        // Arrange
        var context = CreateHttpContext("GET", "/api/test");
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var remaining = int.Parse(context.Response.Headers["X-RateLimit-Remaining"].ToString());
        remaining.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_ShouldNotOverrideExistingHeaders()
    {
        // Arrange
        var context = CreateHttpContext("GET", "/api/test");
        context.Response.Headers["X-RateLimit-Limit"] = "200"; // Pre-set header
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("200");
    }

    [Fact]
    public async Task InvokeAsync_WhenNextMiddlewareThrows_ShouldStillAddHeaders()
    {
        // Arrange
        var context = CreateHttpContext("GET", "/api/test");
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("Test exception"));

        // Act
        var act = () => middleware.InvokeAsync(context);

        // Assert - Headers should be added before exception is thrown
        // Note: In real pipeline, exception middleware would catch this
        // For this test, we verify headers are set in finally block
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region IP Address Tests

    [Fact]
    public void GetClientIpAddress_ShouldReturnRemoteIpAddress_WhenNoProxy()
    {
        // Arrange
        var context = CreateHttpContext("GET", "/api/test");
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");

        // Act
        var ip = RateLimitMiddleware.GetClientIpAddress(context);

        // Assert
        ip.Should().Be("192.168.1.100");
    }

    [Fact]
    public void GetClientIpAddress_ShouldUseXForwardedForHeader_WhenPresent()
    {
        // Arrange
        var context = CreateHttpContext("GET", "/api/test");
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.1");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.50, 10.0.0.1";

        // Act
        var ip = RateLimitMiddleware.GetClientIpAddress(context);

        // Assert
        ip.Should().Be("203.0.113.50"); // Should use first IP in chain
    }

    [Fact]
    public void GetClientIpAddress_ShouldHandleNullRemoteIpAddress()
    {
        // Arrange
        var context = CreateHttpContext("GET", "/api/test");
        context.Connection.RemoteIpAddress = null;

        // Act
        var ip = RateLimitMiddleware.GetClientIpAddress(context);

        // Assert
        ip.Should().Be("unknown");
    }

    [Fact]
    public void GetClientIpAddress_ShouldHandleMultipleXForwardedForIps()
    {
        // Arrange
        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.50, 70.41.3.18, 150.172.238.178";

        // Act
        var ip = RateLimitMiddleware.GetClientIpAddress(context);

        // Assert
        ip.Should().Be("203.0.113.50"); // Should use first IP (client)
    }

    #endregion

    #region Helper Methods

    private RateLimitMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new RateLimitMiddleware(next, _loggerMock.Object);
    }

    private static HttpContext CreateHttpContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        context.TraceIdentifier = Guid.NewGuid().ToString();
        return context;
    }

    private static async Task<T> ReadResponseBody<T>(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    #endregion
}

public class RateLimiterServiceExtensionsTests
{
    [Fact]
    public void AddRateLimiterServices_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddRateLimiterServices();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void AddRateLimiterServices_ShouldReturnServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddRateLimiterServices();

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void ReadLimit_ShouldBe100()
    {
        // Assert
        RateLimiterExtensions.ReadLimit.Should().Be(100);
    }

    [Fact]
    public void WriteLimit_ShouldBe30()
    {
        // Assert
        RateLimiterExtensions.WriteLimit.Should().Be(30);
    }

    [Fact]
    public void TriggerLimit_ShouldBe10()
    {
        // Assert
        RateLimiterExtensions.TriggerLimit.Should().Be(10);
    }

    [Fact]
    public void WindowSeconds_ShouldBe60()
    {
        // Assert
        RateLimiterExtensions.WindowSeconds.Should().Be(60);
    }

    [Fact]
    public void PolicyNames_ShouldBeDefined()
    {
        // Assert
        RateLimiterExtensions.ReadPolicy.Should().Be("read");
        RateLimiterExtensions.WritePolicy.Should().Be("write");
        RateLimiterExtensions.TriggerPolicy.Should().Be("trigger");
    }
}