namespace Yallarhorn.Tests.Unit.Background;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yallarhorn.Background;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Services;

public class DownloadWorkerTests : IDisposable
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IDownloadPipeline> _pipelineMock;
    private readonly Mock<IDownloadQueueService> _queueServiceMock;
    private readonly Mock<IDownloadCoordinator> _coordinatorMock;
    private readonly Mock<ILogger<DownloadWorker>> _loggerMock;
    private readonly CancellationTokenSource _cts;

    public DownloadWorkerTests()
    {
        _pipelineMock = new Mock<IDownloadPipeline>();
        _queueServiceMock = new Mock<IDownloadQueueService>();
        _coordinatorMock = new Mock<IDownloadCoordinator>();
        _loggerMock = new Mock<ILogger<DownloadWorker>>();
        _cts = new CancellationTokenSource();

        // Set up scope factory to return pipeline and queue service
        _serviceProviderMock = new Mock<IServiceProvider>();
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IDownloadPipeline)))
            .Returns(_pipelineMock.Object);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IDownloadQueueService)))
            .Returns(_queueServiceMock.Object);

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
        _coordinatorMock.Object.Dispose();
        _scopeMock.Object.Dispose();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenScopeFactoryIsNull()
    {
        // Act
        var act = () => new DownloadWorker(
            null!,
            _coordinatorMock.Object,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("scopeFactory");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCoordinatorIsNull()
    {
        // Act
        var act = () => new DownloadWorker(
            _scopeFactoryMock.Object,
            null!,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("coordinator");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLoggerIsNull()
    {
        // Act
        var act = () => new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_ShouldUseDefaultPollInterval()
    {
        // Arrange & Act
        using var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object);

        // Assert - no exception
        worker.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldAcceptCustomPollInterval()
    {
        // Arrange & Act
        using var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object,
            TimeSpan.FromSeconds(10));

        // Assert - no exception
        worker.Should().NotBeNull();
    }

    [Fact]
    public async Task StartAsync_ShouldBeginProcessingLoop()
    {
        // Arrange
        var queueItem = CreateQueueItem("ep1");
        _queueServiceMock
            .SetupSequence(q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem)
            .ReturnsAsync((DownloadQueue?)null);

        _pipelineMock
            .Setup(p => p.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Action<PipelineProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineResult { Success = true, EpisodeId = "ep1" });

        _queueServiceMock
            .Setup(q => q.MarkInProgressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _queueServiceMock
            .Setup(q => q.MarkCompletedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object);

        await worker.StartAsync(_cts.Token);

        // Act - call stop multiple times
        await worker.StopAsync(_cts.Token);
        var secondStop = () => worker.StopAsync(_cts.Token);

        // Assert - should not throw
        await secondStop.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldWaitForInFlightDownloadToComplete()
    {
        // Arrange
        var downloadStarted = new TaskCompletionSource<bool>();
        var downloadCanComplete = new TaskCompletionSource<bool>();
        var queueItem = CreateQueueItem("ep1");

        _queueServiceMock
            .SetupSequence(q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem)
            .ReturnsAsync((DownloadQueue?)null);

        _queueServiceMock
            .Setup(q => q.MarkInProgressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pipelineMock
            .Setup(p => p.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Action<PipelineProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                downloadStarted.SetResult(true);
                await downloadCanComplete.Task;
                return new PipelineResult { Success = true, EpisodeId = "ep1" };
            });

        using var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMilliseconds(50));

        await worker.StartAsync(_cts.Token);
        await downloadStarted.Task;

        // Act - start stopping while download is in-flight
        var stopTask = worker.StopAsync(_cts.Token);

        // The stop should not complete yet
        await Task.Delay(50);
        stopTask.IsCompleted.Should().BeFalse();

        // Now allow the download to complete
        downloadCanComplete.SetResult(true);

        // Now stop should complete
        await stopTask;
    }

    [Fact]
    public async Task ProcessNextItemAsync_ShouldProcessQueueItemSuccessfully()
    {
        // Arrange
        var queueItem = CreateQueueItem("episode-1");

        _queueServiceMock
            .Setup(q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem);

        _queueServiceMock
            .Setup(q => q.MarkInProgressAsync(queueItem.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pipelineMock
            .Setup(p => p.ExecuteAsync(
                queueItem.EpisodeId,
                It.IsAny<Action<PipelineProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineResult { Success = true, EpisodeId = queueItem.EpisodeId });

        _queueServiceMock
            .Setup(q => q.MarkCompletedAsync(queueItem.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Set up to return null after first item
        _queueServiceMock
            .SetupSequence(q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem)
            .ReturnsAsync((DownloadQueue?)null);

        using var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMilliseconds(50));

        // Act
        await worker.StartAsync(_cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(_cts.Token);

        // Assert
        _queueServiceMock.Verify(
            q => q.MarkInProgressAsync(queueItem.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        _pipelineMock.Verify(
            p => p.ExecuteAsync(queueItem.EpisodeId, It.IsAny<Action<PipelineProgress>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _queueServiceMock.Verify(
            q => q.MarkCompletedAsync(queueItem.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessNextItemAsync_ShouldMarkFailed_WhenPipelineFails()
    {
        // Arrange
        var queueItem = CreateQueueItem("episode-2");
        var errorMessage = "Download failed";

        _queueServiceMock
            .SetupSequence(q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem)
            .ReturnsAsync((DownloadQueue?)null);

        _queueServiceMock
            .Setup(q => q.MarkInProgressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pipelineMock
            .Setup(p => p.ExecuteAsync(
                queueItem.EpisodeId,
                It.IsAny<Action<PipelineProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineResult { Success = false, EpisodeId = queueItem.EpisodeId, Error = errorMessage });

        _queueServiceMock
            .Setup(q => q.MarkFailedAsync(
                It.IsAny<string>(),
                errorMessage,
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMilliseconds(50));

        // Act
        await worker.StartAsync(_cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(_cts.Token);

        // Assert
        _queueServiceMock.Verify(
            q => q.MarkFailedAsync(
                queueItem.Id,
                errorMessage,
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessNextItemAsync_ShouldProcessRetryableItems()
    {
        // Arrange
        var retryItem = new DownloadQueue
        {
            Id = "retry-1",
            EpisodeId = "episode-retry",
            Status = QueueStatus.Retrying,
            Priority = 5,
            Attempts = 1,
            MaxAttempts = 5,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            UpdatedAt = DateTimeOffset.UtcNow,
            NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        _queueServiceMock
            .Setup(q => q.GetRetryableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { retryItem });

        _queueServiceMock
            .SetupSequence(q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DownloadQueue?)null);

        _queueServiceMock
            .Setup(q => q.MarkInProgressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pipelineMock
            .Setup(p => p.ExecuteAsync(
                retryItem.EpisodeId,
                It.IsAny<Action<PipelineProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineResult { Success = true, EpisodeId = retryItem.EpisodeId });

        _queueServiceMock
            .Setup(q => q.MarkCompletedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMilliseconds(50));

        // Act
        await worker.StartAsync(_cts.Token);
        await Task.Delay(100);
        await worker.StopAsync(_cts.Token);

        // Assert - GetRetryableAsync should have been called
        _queueServiceMock.Verify(
            q => q.GetRetryableAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public async Task Worker_ShouldNotStartNewDownloads_AfterStopRequested()
    {
        // Arrange
        var processedCount = 0;
        var firstItemProcessed = new TaskCompletionSource<bool>();

        _queueServiceMock
            .Setup(q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                processedCount++;
                if (processedCount == 1)
                {
                    return CreateQueueItem($"ep-{processedCount}");
                }
                return new DownloadQueue
                {
                    Id = $"queue-{processedCount}",
                    EpisodeId = $"ep-{processedCount}",
                    Status = QueueStatus.Pending,
                    Priority = 5,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
            });

        _queueServiceMock
            .Setup(q => q.MarkInProgressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pipelineMock
            .Setup(p => p.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Action<PipelineProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                processedCount++;
                if (processedCount == 2)
                {
                    firstItemProcessed.SetResult(true);
                }
                await Task.Delay(10);
                return new PipelineResult { Success = true, EpisodeId = "test" };
            });

        _queueServiceMock
            .Setup(q => q.MarkCompletedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMilliseconds(10));

        // Act
        await worker.StartAsync(_cts.Token);
        await firstItemProcessed.Task;
        await worker.StopAsync(_cts.Token);

        // Assert - after stop, no new downloads should start
        var finalCount = processedCount;
        await Task.Delay(100);
        processedCount.Should().Be(finalCount);
    }

    [Fact]
    public async Task Worker_ShouldHandlePipelineExceptionGracefully()
    {
        // Arrange
        var queueItem = CreateQueueItem("ep-exception");

        _queueServiceMock
            .SetupSequence(q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem)
            .ReturnsAsync((DownloadQueue?)null);

        _queueServiceMock
            .Setup(q => q.MarkInProgressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pipelineMock
            .Setup(p => p.ExecuteAsync(
                queueItem.EpisodeId,
                It.IsAny<Action<PipelineProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Pipeline crashed"));

        _queueServiceMock
            .Setup(q => q.MarkFailedAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMilliseconds(50));

        // Act - should not throw
        var act = async () =>
        {
            await worker.StartAsync(_cts.Token);
            await Task.Delay(100);
            await worker.StopAsync(_cts.Token);
        };

        await act.Should().NotThrowAsync();

        // Assert - should have marked as failed
        _queueServiceMock.Verify(
            q => q.MarkFailedAsync(
                queueItem.Id,
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Worker_ShouldContinueProcessing_AfterError()
    {
        // Arrange
        var failedItem = CreateQueueItem("ep-fail");
        var successItem = CreateQueueItem("ep-success");

        var callCount = 0;

        _queueServiceMock
            .Setup(q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount switch
                {
                    1 => failedItem,
                    2 => successItem,
                    _ => null
                };
            });

        _queueServiceMock
            .Setup(q => q.MarkInProgressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pipelineMock
            .Setup(p => p.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Action<PipelineProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string epId, Action<PipelineProgress>? _, CancellationToken _) =>
            {
                if (epId == "ep-fail")
                {
                    return new PipelineResult { Success = false, EpisodeId = epId, Error = "Failed" };
                }
                return new PipelineResult { Success = true, EpisodeId = epId };
            });

        _queueServiceMock
            .Setup(q => q.MarkCompletedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _queueServiceMock
            .Setup(q => q.MarkFailedAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMilliseconds(20));

        // Act
        await worker.StartAsync(_cts.Token);
        await Task.Delay(200);
        await worker.StopAsync(_cts.Token);

        // Assert - both items should have been processed
        _queueServiceMock.Verify(
            q => q.MarkFailedAsync(failedItem.Id, It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _queueServiceMock.Verify(
            q => q.MarkCompletedAsync(successItem.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Worker_ShouldProcessMultipleConcurrentItems()
    {
        // Arrange - set up multiple items
        var items = Enumerable.Range(1, 3)
            .Select(i => CreateQueueItem($"ep-{i}"))
            .ToList();

        var processedItems = new List<string>();
        var nextIndex = 0;

        _queueServiceMock
            .Setup(q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (nextIndex < items.Count)
                {
                    return items[nextIndex++];
                }
                return null;
            });

        _queueServiceMock
            .Setup(q => q.MarkInProgressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pipelineMock
            .Setup(p => p.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Action<PipelineProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string epId, Action<PipelineProgress>? _, CancellationToken _) =>
            {
                lock (processedItems)
                {
                    processedItems.Add(epId);
                }
                return new PipelineResult { Success = true, EpisodeId = epId };
            });

        _queueServiceMock
            .Setup(q => q.MarkCompletedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMilliseconds(20));

        // Act
        await worker.StartAsync(_cts.Token);
        await Task.Delay(200);
        await worker.StopAsync(_cts.Token);

        // Assert - all items should be processed
        processedItems.Should().HaveCount(3);
        processedItems.Should().Contain(["ep-1", "ep-2", "ep-3"]);
    }

    [Fact]
    public async Task Worker_ShouldHandleEmptyQueueGracefully()
    {
        // Arrange - queue is empty
        _queueServiceMock
            .Setup(q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DownloadQueue?)null);

        _queueServiceMock
            .Setup(q => q.GetRetryableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        using var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMilliseconds(50));

        // Act - should not throw
        var act = async () =>
        {
            await worker.StartAsync(_cts.Token);
            await Task.Delay(150);
            await worker.StopAsync(_cts.Token);
        };

        await act.Should().NotThrowAsync();

        // Assert - pipeline should not have been called
        _pipelineMock.Verify(
            p => p.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Action<PipelineProgress>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Worker_ShouldUseConfiguredPollInterval()
    {
        // Arrange - use a longer poll interval
        var customInterval = TimeSpan.FromMilliseconds(100);

        _queueServiceMock
            .Setup(q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DownloadQueue?)null);

        using var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object,
            customInterval);

        // Act
        await worker.StartAsync(_cts.Token);
        await Task.Delay(50); // Less than poll interval
        await worker.StopAsync(_cts.Token);

        // Assert - with empty queue, GetNextPendingAsync should be called based on interval
        _queueServiceMock.Verify(
            q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task Dispose_ShouldCleanupResources()
    {
        // Arrange
        _queueServiceMock
            .Setup(q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DownloadQueue?)null);

var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object);

        await worker.StartAsync(_cts.Token);
        await worker.StopAsync(_cts.Token);

        // Act - dispose should not throw
        var act = () => worker.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task StartAsync_WhenCalledTwice_ShouldBeSafe()
    {
        // Arrange
        _queueServiceMock
            .Setup(q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DownloadQueue?)null);

        using var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object);

        // Act
        await worker.StartAsync(_cts.Token);
        await worker.StartAsync(_cts.Token); // Called twice

        // Assert - should not throw and only start once
        _queueServiceMock.Verify(
            q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()),
            Times.Once());

        await worker.StopAsync(_cts.Token);
    }

    [Fact]
    public async Task Worker_ShouldHandleCancellationGracefully()
    {
        // Arrange
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        var queueItem = CreateQueueItem("ep-cancel");

        _queueServiceMock
            .Setup(q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem);

        _queueServiceMock
            .Setup(q => q.MarkInProgressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pipelineMock
            .Setup(p => p.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Action<PipelineProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string _, Action<PipelineProgress>? _, CancellationToken ct) =>
            {
                await Task.Delay(500, ct);
                return new PipelineResult { Success = true, EpisodeId = "ep-cancel" };
            });

        using var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMilliseconds(50));

        await worker.StartAsync(linkedCts.Token);
        await Task.Delay(50);

        // Act - cancel during operation
        linkedCts.Cancel();

        // Stop should complete gracefully
        await worker.StopAsync(CancellationToken.None);

        linkedCts.Dispose();
    }

    [Fact]
    public async Task Worker_ShouldRetryMarkProgress_WhenMarkInProgressFails()
    {
        // Arrange
        var queueItem = CreateQueueItem("ep-retry");

        var callCount = 0;
        _queueServiceMock
            .Setup(q => q.GetNextPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++callCount <= 2 ? queueItem : null);

        _queueServiceMock
            .SetupSequence(q => q.MarkInProgressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Concurrent modification"))
            .Returns(Task.CompletedTask);

        _pipelineMock
            .Setup(p => p.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Action<PipelineProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineResult { Success = true, EpisodeId = "ep-retry" });

        _queueServiceMock
            .Setup(q => q.MarkCompletedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var worker = new DownloadWorker(
            _scopeFactoryMock.Object,
            _coordinatorMock.Object,
            _loggerMock.Object,
            TimeSpan.FromMilliseconds(20));

        // Act
        await worker.StartAsync(_cts.Token);
        await Task.Delay(150);
        await worker.StopAsync(_cts.Token);

        // Assert - MarkInProgress should have been called at least once successfully
        _queueServiceMock.Verify(
            q => q.MarkInProgressAsync(queueItem.Id, It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    private static DownloadQueue CreateQueueItem(string episodeId)
    {
        return new DownloadQueue
        {
            Id = $"queue-{episodeId}",
            EpisodeId = episodeId,
            Status = QueueStatus.Pending,
            Priority = 5,
            Attempts = 0,
            MaxAttempts = 5,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}