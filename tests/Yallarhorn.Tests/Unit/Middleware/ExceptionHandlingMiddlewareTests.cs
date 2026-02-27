namespace Yallarhorn.Tests.Unit.Middleware;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;
using System.Text.Json;
using Xunit;
using Yallarhorn.Middleware;
using Yallarhorn.Models;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _loggerMock;
    private readonly Mock<IWebHostEnvironment> _environmentMock;

    public ExceptionHandlingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        _environmentMock = new Mock<IWebHostEnvironment>();
    }

    [Fact]
    public async Task InvokeAsync_WhenNoException_PassesThroughToNext()
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
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_WhenArgumentException_Returns400()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new ArgumentException("Invalid argument"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(400);
        var errorDetails = await ReadResponseBody<ErrorDetails>(context);
        errorDetails.Status.Should().Be(400);
        errorDetails.Error.Should().Be("Bad Request");
        errorDetails.RequestId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenArgumentNullException_Returns400WithParamName()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new ArgumentNullException("testParam"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(400);
        var errorDetails = await ReadResponseBody<ErrorDetails>(context);
        errorDetails.Status.Should().Be(400);
        errorDetails.Detail.Should().Contain("testParam");
        errorDetails.Detail.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task InvokeAsync_WhenKeyNotFoundException_Returns404()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new KeyNotFoundException("Not found"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(404);
        var errorDetails = await ReadResponseBody<ErrorDetails>(context);
        errorDetails.Status.Should().Be(404);
        errorDetails.Error.Should().Be("Not Found");
        errorDetails.Detail.Should().Contain("not found");
    }

    [Fact]
    public async Task InvokeAsync_WhenInvalidOperationException_Returns404()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("Invalid operation"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(404);
        var errorDetails = await ReadResponseBody<ErrorDetails>(context);
        errorDetails.Status.Should().Be(404);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnauthorizedAccessException_Returns401()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new UnauthorizedAccessException("Unauthorized"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(401);
        var errorDetails = await ReadResponseBody<ErrorDetails>(context);
        errorDetails.Status.Should().Be(401);
        errorDetails.Error.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task InvokeAsync_WhenTimeoutException_Returns408()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new TimeoutException("Operation timed out"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(408);
        var errorDetails = await ReadResponseBody<ErrorDetails>(context);
        errorDetails.Status.Should().Be(408);
        errorDetails.Error.Should().Be("Request Timeout");
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledException_Returns500()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new Exception("Something went wrong"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(500);
        var errorDetails = await ReadResponseBody<ErrorDetails>(context);
        errorDetails.Status.Should().Be(500);
        errorDetails.Error.Should().Be("Internal Server Error");
    }

    [Fact]
    public async Task InvokeAsync_InDevelopment_IncludesStackTrace()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new Exception("Test error"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var errorDetails = await ReadResponseBody<ErrorDetails>(context);
        errorDetails.StackTrace.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_InProduction_ExcludesStackTrace()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new Exception("Test error"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var errorDetails = await ReadResponseBody<ErrorDetails>(context);
        errorDetails.StackTrace.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_WithCustomRequestId_UsesCustomRequestId()
    {
        // Arrange
        var customRequestId = "custom-request-id-123";
        var context = CreateHttpContext();
        context.Request.Headers["X-Request-ID"] = customRequestId;
        var middleware = CreateMiddleware(_ => throw new Exception("Test error"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var errorDetails = await ReadResponseBody<ErrorDetails>(context);
        errorDetails.RequestId.Should().Be(customRequestId);
    }

    [Fact]
    public async Task InvokeAsync_WithTraceIdHeader_UsesTraceId()
    {
        // Arrange
        var traceId = "trace-id-456";
        var context = CreateHttpContext();
        context.Request.Headers["X-Trace-ID"] = traceId;
        var middleware = CreateMiddleware(_ => throw new Exception("Test error"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var errorDetails = await ReadResponseBody<ErrorDetails>(context);
        errorDetails.RequestId.Should().Be(traceId);
    }

    [Fact]
    public async Task InvokeAsync_WithoutCustomHeaders_UsesTraceIdentifier()
    {
        // Arrange
        var context = CreateHttpContext();
        var expectedTraceId = context.TraceIdentifier;
        var middleware = CreateMiddleware(_ => throw new Exception("Test error"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var errorDetails = await ReadResponseBody<ErrorDetails>(context);
        errorDetails.RequestId.Should().Be(expectedTraceId);
    }

    [Fact]
    public async Task InvokeAsync_SetsContentTypeToJson()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new Exception("Test error"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task InvokeAsync_IncludesTimestamp()
    {
        // Arrange
        var beforeTime = DateTime.UtcNow.AddSeconds(-1);
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new Exception("Test error"));

        // Act
        await middleware.InvokeAsync(context);
        var afterTime = DateTime.UtcNow.AddSeconds(1);

        // Assert
        var errorDetails = await ReadResponseBody<ErrorDetails>(context);
        errorDetails.Timestamp.Should().BeAfter(beforeTime);
        errorDetails.Timestamp.Should().BeBefore(afterTime);
    }

    [Fact]
    public async Task InvokeAsync_LogsError()
    {
        // Arrange
        var context = CreateHttpContext();
        var exception = new Exception("Test error");
        var middleware = CreateMiddleware(_ => throw exception);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_XRequestIDTakesPrecedenceOverXTraceID()
    {
        // Arrange
        var requestId = "request-id-123";
        var traceId = "trace-id-456";
        var context = CreateHttpContext();
        context.Request.Headers["X-Request-ID"] = requestId;
        context.Request.Headers["X-Trace-ID"] = traceId;
        var middleware = CreateMiddleware(_ => throw new Exception("Test error"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var errorDetails = await ReadResponseBody<ErrorDetails>(context);
        errorDetails.RequestId.Should().Be(requestId);
    }

    private ExceptionHandlingMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new ExceptionHandlingMiddleware(next, _environmentMock.Object, _loggerMock.Object);
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
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
}

public class ErrorDetailsTests
{
    [Fact]
    public void Constructor_Default_SetsDefaultValues()
    {
        // Arrange & Act
        var details = new ErrorDetails();

        // Assert
        details.Status.Should().Be(0);
        details.Error.Should().BeEmpty();
        details.Detail.Should().BeNull();
        details.RequestId.Should().BeNull();
        details.StackTrace.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithParameters_SetsProperties()
    {
        // Arrange & Act
        var details = new ErrorDetails(404, "Not Found", "Resource does not exist");

        // Assert
        details.Status.Should().Be(404);
        details.Error.Should().Be("Not Found");
        details.Detail.Should().Be("Resource does not exist");
    }

    [Fact]
    public void Timestamp_IsSetToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var details = new ErrorDetails();
        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        details.Timestamp.Should().BeAfter(before);
        details.Timestamp.Should().BeBefore(after);
    }

    [Fact]
    public void ToString_ReturnsValidJson()
    {
        // Arrange
        var details = new ErrorDetails(400, "Bad Request", "Invalid parameter");

        // Act
        var json = details.ToString();

        // Assert
        json.Should().Contain("\"status\":400");
        json.Should().Contain("\"error\":\"Bad Request\"");
        json.Should().Contain("\"detail\":\"Invalid parameter\"");
    }

    [Fact]
    public void StackTrace_WhenNull_IsNotSerialized()
    {
        // Arrange
        var details = new ErrorDetails(500, "Error")
        {
            StackTrace = null
        };

        // Act
        var json = details.ToString();

        // Assert
        json.Should().NotContain("stackTrace");
    }

    [Fact]
    public void StackTrace_WhenSet_IsSerialized()
    {
        // Arrange
        var details = new ErrorDetails(500, "Error")
        {
            StackTrace = "at Method() in File.cs:line 1"
        };

        // Act
        var json = details.ToString();

        // Assert
        json.Should().Contain("stackTrace");
        json.Should().Contain("at Method()");
    }
}