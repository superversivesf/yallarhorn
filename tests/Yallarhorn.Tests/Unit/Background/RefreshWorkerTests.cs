namespace Yallarhorn.Tests.Unit.Background;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yallarhorn.Background;
using Yallarhorn.Services;

public class RefreshWorkerTests : IDisposable
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IChannelRefreshService> _refreshServiceMock;
    private readonly Mock<ILogger<RefreshWorker>> _loggerMock;
    private readonly CancellationTokenSource _cts;

    public RefreshWorkerTests()
    {
        _refreshServiceMock = new Mock<IChannelRefreshService>();
        _loggerMock = new Mock<ILogger<RefreshWorker>>();
        _cts = new CancellationTokenSource();

        // Set up scope factory to return refresh service
        _serviceProviderMock = new Mock<IServiceProvider>();
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IChannelRefreshService)))
            .Returns(_refreshServiceMock.Object);

        _scopeMock = new Mock<IServiceScope>();
        _scopeMock
            .Setup(s => s.ServiceProvider)
            .Returns(_serviceProviderMock.Object);

        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Returns(_scopeMock.Object);
    }

    public void Dispose()
    {
        _cts.Dispose();
        _scopeMock.Object.Dispose();
    }

    [Fact]
    public async Task StartAsync_ShouldTriggerInitialRefresh()
    {
        // Arrange
        var results = new List<RefreshResult>();
        _refreshServiceMock
            .Setup(r => r.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        using var worker = new RefreshWorker(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMinutes(60));

        // Act
        await worker.StartAsync(_cts.Token);

        // Assert - initial refresh should be triggered
        _refreshServiceMock.Verify(
            r => r.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        // Cleanup
        await worker.StopAsync(_cts.Token);
    }

    [Fact]
    public async Task StartAsync_ShouldNotThrow_WhenInitialRefreshFails()
    {
        // Arrange
        _refreshServiceMock
            .Setup(r => r.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Refresh failed"));

        using var worker = new RefreshWorker(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMinutes(60));

        // Act - should not throw
        var act = () => worker.StartAsync(_cts.Token);
        await act.Should().NotThrowAsync();

        // Cleanup
        await worker.StopAsync(_cts.Token);
    }

    [Fact]
    public async Task RefreshAllChannelsAsync_ShouldBeCalled_Periodically()
    {
        // Arrange
        var refreshCount = 0;
        _refreshServiceMock
            .Setup(r => r.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()))
            .Callback(() => refreshCount++)
            .ReturnsAsync(new List<RefreshResult>());

        // Use a very short interval for testing
        var interval = TimeSpan.FromMilliseconds(100);
        using var worker = new RefreshWorker(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            interval);

        // Act
        await worker.StartAsync(_cts.Token);

        // Wait for initial refresh + at least 2 more cycles
        await Task.Delay(350);

        // Stop the worker
        await worker.StopAsync(_cts.Token);

        // Assert - should have initial + at least 2 periodic refreshes
        refreshCount.Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task StopAsync_ShouldDisposeTimerAndCompleteGracefully()
    {
        // Arrange
        _refreshServiceMock
            .Setup(r => r.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefreshResult>());

        using var worker = new RefreshWorker(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMinutes(60));

        await worker.StartAsync(_cts.Token);

        // Act - should complete without exception
        var act = () => worker.StopAsync(_cts.Token);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldWaitForInFlightRefreshToComplete()
    {
        // Arrange
        var refreshStarted = new TaskCompletionSource<bool>();
        var refreshCanComplete = new TaskCompletionSource<bool>();

        _refreshServiceMock
            .Setup(r => r.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                refreshStarted.SetResult(true);
                await refreshCanComplete.Task;
                return new List<RefreshResult>();
            });

        using var worker = new RefreshWorker(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMinutes(60));

        await worker.StartAsync(_cts.Token);

        // Wait for the initial refresh to start
        await refreshStarted.Task;

        // Act - start stopping while refresh is in-flight
        var stopTask = worker.StopAsync(_cts.Token);

        // The stop should not complete yet
        await Task.Delay(50);
        stopTask.IsCompleted.Should().BeFalse();

        // Now allow the refresh to complete
        refreshCanComplete.SetResult(true);

        // Now stop should complete
        await stopTask;
    }

    [Fact]
    public async Task ErrorIsolation_OneChannelFailureShouldNotStopOtherChannels()
    {
        // This test verifies that RefreshWorker calls RefreshAllChannelsAsync
        // which already has error isolation built-in. We verify the worker handles
        // errors gracefully.

        // Arrange
        _refreshServiceMock
            .SetupSequence(r => r.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Channel error"))
            .ReturnsAsync(new List<RefreshResult> { new() { ChannelId = "ch1", VideosFound = 5, EpisodesQueued = 2 } });

        using var worker = new RefreshWorker(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMilliseconds(50));

        // Act
        await worker.StartAsync(_cts.Token);
        await Task.Delay(150);
        await worker.StopAsync(_cts.Token);

        // Assert - worker should have continued after first failure
        _refreshServiceMock.Verify(
            r => r.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task StartAsync_ShouldUseConfiguredPollInterval()
    {
        // Arrange - use a custom interval
        var customInterval = TimeSpan.FromSeconds(30);
        _refreshServiceMock
            .Setup(r => r.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefreshResult>());

        using var worker = new RefreshWorker(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            customInterval);

        // Act
        await worker.StartAsync(_cts.Token);
        await Task.Delay(50); // Let the startup complete

        // Assert - interval is used internally, verify via behavior
        // Initial call happens immediately
        _refreshServiceMock.Verify(
            r => r.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        await worker.StopAsync(_cts.Token);
    }

    [Fact]
    public async Task StopAsync_ShouldBeIdempotent()
    {
        // Arrange
        _refreshServiceMock
            .Setup(r => r.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefreshResult>());

        using var worker = new RefreshWorker(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMinutes(60));

        await worker.StartAsync(_cts.Token);

        // Act - call stop multiple times
        await worker.StopAsync(_cts.Token);
        var secondStop = () => worker.StopAsync(_cts.Token);

        // Assert - should not throw
        await secondStop.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_WhenCalledTwice_ShouldBeSafe()
    {
        // Arrange
        _refreshServiceMock
            .Setup(r => r.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefreshResult>());

        using var worker = new RefreshWorker(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMinutes(60));

        // Act
        await worker.StartAsync(_cts.Token);
        await worker.StartAsync(_cts.Token); // Called twice

        // Assert - only one initial refresh should occur
        _refreshServiceMock.Verify(
            r => r.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        await worker.StopAsync(_cts.Token);
    }

    [Fact]
    public async Task Dispose_ShouldCleanupResources()
    {
        // Arrange
        _refreshServiceMock
            .Setup(r => r.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefreshResult>());

        var worker = new RefreshWorker(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMinutes(60));

        await worker.StartAsync(_cts.Token);
        await worker.StopAsync(_cts.Token);

        // Act - dispose should not throw
        var act = () => worker.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_ShouldAcceptDefaultPollInterval()
    {
        // Arrange & Act
        using var worker = new RefreshWorker(
            _scopeFactoryMock.Object,
            _loggerMock.Object);

        // Assert - no exception
        worker.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshWorker_ShouldHandleCancellationGracefully()
    {
        // Arrange
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        _refreshServiceMock
            .Setup(r => r.RefreshAllChannelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefreshResult>());

        using var worker = new RefreshWorker(
            _scopeFactoryMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMilliseconds(50));

        await worker.StartAsync(linkedCts.Token);
        await Task.Delay(100);

        // Act - cancel during operation
        linkedCts.Cancel();

        // Stop should complete
        await worker.StopAsync(CancellationToken.None); // Use None since we cancelled the other

        linkedCts.Dispose();
    }
}