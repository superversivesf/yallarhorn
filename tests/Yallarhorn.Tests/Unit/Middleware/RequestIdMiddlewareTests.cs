namespace Yallarhorn.Tests.Unit.Middleware;

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;
using Xunit;
using Yallarhorn.Middleware;

public class RequestIdMiddlewareTests
{
    private readonly Mock<ILogger<RequestIdMiddleware>> _loggerMock;

    public RequestIdMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<RequestIdMiddleware>>();
    }

    [Fact]
    public async Task InvokeAsync_WithNoRequestIdHeader_GeneratesNewRequestId()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey("X-Request-ID");
        var requestId = context.Response.Headers["X-Request-ID"].ToString();
        requestId.Should().NotBeNullOrEmpty();
        Guid.TryParse(requestId, out _).Should().BeTrue("Request ID should be a valid GUID");
    }

    [Fact]
    public async Task InvokeAsync_WithExistingRequestIdHeader_UsesExistingId()
    {
        // Arrange
        var existingRequestId = Guid.NewGuid().ToString();
        var context = CreateHttpContext();
        context.Request.Headers["X-Request-ID"] = existingRequestId;
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Request-ID"].ToString().Should().Be(existingRequestId);
    }

    [Fact]
    public async Task InvokeAsync_PassesThroughToNext()
    {
        // Arrange
        var wasCalled = false;
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ =>
        {
            wasCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        wasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_SetsRequestIdOnHttpContextItems()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items.Should().ContainKey("RequestId");
        context.Items["RequestId"].Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WithCustomRequestId_PreservesValue()
    {
        // Arrange
        var customRequestId = "custom-request-123";
        var context = CreateHttpContext();
        context.Request.Headers["X-Request-ID"] = customRequestId;
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Request-ID"].ToString().Should().Be(customRequestId);
        context.Items["RequestId"]!.ToString().Should().Be(customRequestId);
    }

    [Fact]
    public async Task InvokeAsync_AddsLoggerScope()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - Verify logger scope was created with request ID
        // The middleware should add scope with RequestId
        _loggerMock.Verify(
            x => x.BeginScope(It.IsAny<It.IsAnyType>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_MultipleRequests_GenerateDifferentIds()
    {
        // Arrange
        var context1 = CreateHttpContext();
        var context2 = CreateHttpContext();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context1);
        await middleware.InvokeAsync(context2);

        // Assert
        var requestId1 = context1.Response.Headers["X-Request-ID"].ToString();
        var requestId2 = context2.Response.Headers["X-Request-ID"].ToString();
        requestId1.Should().NotBe(requestId2, "Each request should get a unique ID");
    }

    [Fact]
    public async Task InvokeAsync_WithTraceIdentifier_Fallback()
    {
        // Arrange - ASP.NET Core sets TraceIdentifier on the HttpContext
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - Should use generated GUID, not TraceIdentifier (TraceIdentifier is fallback in middleware)
        context.Response.Headers.Should().ContainKey("X-Request-ID");
        context.Response.Headers["X-Request-ID"].ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_LogsRequestWithRequestId()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - Verify log calls were made
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private RequestIdMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new RequestIdMiddleware(next, _loggerMock.Object);
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = Guid.NewGuid().ToString();
        return context;
    }
}