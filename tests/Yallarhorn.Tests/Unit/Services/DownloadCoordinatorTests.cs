namespace Yallarhorn.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yallarhorn.Services;

public class DownloadCoordinatorTests
{
    private readonly Mock<ILogger<DownloadCoordinator>> _loggerMock;
    private readonly int _defaultMaxConcurrent = 3;

    public DownloadCoordinatorTests()
    {
        _loggerMock = new Mock<ILogger<DownloadCoordinator>>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithDefaultMaxConcurrent_ShouldInitializeCorrectly()
    {
        // Act
        var coordinator = new DownloadCoordinator(_loggerMock.Object);

        // Assert
        coordinator.MaxConcurrentDownloads.Should().Be(_defaultMaxConcurrent);
        coordinator.AvailableSlots.Should().Be(_defaultMaxConcurrent);
        coordinator.ActiveDownloads.Should().Be(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void Constructor_WithValidMaxConcurrent_ShouldInitializeCorrectly(int maxConcurrent)
    {
        // Act
        var coordinator = new DownloadCoordinator(_loggerMock.Object, maxConcurrent);

        // Assert
        coordinator.MaxConcurrentDownloads.Should().Be(maxConcurrent);
        coordinator.AvailableSlots.Should().Be(maxConcurrent);
        coordinator.ActiveDownloads.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Constructor_WithInvalidMaxConcurrent_ShouldUseMinimum(int invalidMaxConcurrent)
    {
        // Act
        var coordinator = new DownloadCoordinator(_loggerMock.Object, invalidMaxConcurrent);

        // Assert
        coordinator.MaxConcurrentDownloads.Should().Be(1);
        coordinator.AvailableSlots.Should().Be(1);
    }

    #endregion

    #region AcquireSlotAsync/ReleaseSlot Tests

    [Fact]
    public async Task AcquireSlotAsync_WhenSlotAvailable_ShouldAcquireImmediately()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 3);

        // Act
        await coordinator.AcquireSlotAsync();

        // Assert
        coordinator.ActiveDownloads.Should().Be(1);
        coordinator.AvailableSlots.Should().Be(2);
    }

    [Fact]
    public async Task AcquireSlotAsync_WhenAllSlotsTaken_ShouldWaitForRelease()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 1);
        var acquireTask1 = coordinator.AcquireSlotAsync();

        // Act - Start second acquire before releasing
        var acquireTask2 = coordinator.AcquireSlotAsync();
        
        // Should be waiting since only 1 slot and it's taken
        acquireTask2.IsCompleted.Should().BeFalse();

        // Release the first slot
        coordinator.ReleaseSlot();

        // Now second acquire should complete
        await acquireTask2.WaitAsync(TimeSpan.FromSeconds(1));

        // Assert
        coordinator.ActiveDownloads.Should().Be(1);
        coordinator.AvailableSlots.Should().Be(0);

        acquireTask1.Dispose();
        acquireTask2.Dispose();
    }

    [Fact]
    public async Task ReleaseSlot_WhenSlotAcquired_ShouldIncrementAvailable()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 3);
        await coordinator.AcquireSlotAsync();

        // Act
        coordinator.ReleaseSlot();

        // Assert
        coordinator.ActiveDownloads.Should().Be(0);
        coordinator.AvailableSlots.Should().Be(3);
    }

    [Fact]
    public async Task AcquireSlotAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 1);
        await coordinator.AcquireSlotAsync(); // Take the only slot

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => coordinator.AcquireSlotAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task AcquireSlotAsync_MultipleAcquisitions_ShouldTrackCorrectly()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 3);

        // Act
        await coordinator.AcquireSlotAsync();
        await coordinator.AcquireSlotAsync();
        await coordinator.AcquireSlotAsync();

        // Assert
        coordinator.ActiveDownloads.Should().Be(3);
        coordinator.AvailableSlots.Should().Be(0);
    }

    #endregion

    #region ExecuteDownloadAsync Tests

    [Fact]
    public async Task ExecuteDownloadAsync_ShouldAcquireAndReleaseSlot()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 3);
        var executed = false;

        // Act
        var result = await coordinator.ExecuteDownloadAsync(async () =>
        {
            executed = true;
            coordinator.ActiveDownloads.Should().Be(1);
            return 42;
        });

        // Assert
        result.Should().Be(42);
        executed.Should().BeTrue();
        coordinator.ActiveDownloads.Should().Be(0);
        coordinator.AvailableSlots.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteDownloadAsync_WhenExceptionThrown_ShouldReleaseSlotAndPropagate()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 3);

        // Act
        var act = () => coordinator.ExecuteDownloadAsync<int>(() =>
            throw new InvalidOperationException("Test error"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test error");
        
        coordinator.ActiveDownloads.Should().Be(0);
        coordinator.AvailableSlots.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteDownloadAsync_WhenCancelled_ShouldReleaseSlotAndThrow()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 3);
        var cts = new CancellationTokenSource();

        // Act
        var act = () => coordinator.ExecuteDownloadAsync(async ct =>
        {
            ct.ThrowIfCancellationRequested();
            return 42;
        }, cts.Token);

        // Not cancelled yet
        var result = await act();
        result.Should().Be(42);
        
        coordinator.ActiveDownloads.Should().Be(0);
        coordinator.AvailableSlots.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteDownloadAsync_ConcurrentExecutions_ShouldLimitConcurrency()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 2);
        var maxConcurrent = 0;
        var currentConcurrent = 0;
        var lockObj = new object();

        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            return await coordinator.ExecuteDownloadAsync(async () =>
            {
                int current;
                lock (lockObj)
                {
                    currentConcurrent++;
                    if (currentConcurrent > maxConcurrent)
                    {
                        maxConcurrent = currentConcurrent;
                    }
                    current = currentConcurrent;
                }

                await Task.Delay(100); // Simulate work

                lock (lockObj)
                {
                    currentConcurrent--;
                }

                return current;
            });
        }).ToList();

        // Act
        await Task.WhenAll(tasks);

        // Assert - Max concurrency observed should not exceed 2
        maxConcurrent.Should().BeLessOrEqualTo(2);
        
        // All slots should be available after
        coordinator.ActiveDownloads.Should().Be(0);
        coordinator.AvailableSlots.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteDownloadAsync_WithResult_ShouldReturnResult()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 3);

        // Act
        var result = await coordinator.ExecuteDownloadAsync(() => Task.FromResult("success"));

        // Assert
        result.Should().Be("success");
    }

    [Fact]
    public async Task ExecuteDownloadAsync_WithVoidTask_ShouldWork()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 3);
        var executed = false;

        // Act
        await coordinator.ExecuteDownloadAsync(() =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        // Assert
        executed.Should().BeTrue();
    }

    #endregion

    #region Concurrency Limits Tests

    [Fact]
    public async Task AvailableSlots_WhenFull_ShouldBlock()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 2);
        var tcs1 = new TaskCompletionSource();
        var tcs2 = new TaskCompletionSource();
        var waitTask = new List<Task>();

        // Take both slots
        var task1 = coordinator.AcquireSlotAsync();
        var task2 = coordinator.AcquireSlotAsync();

        // Start a task that will wait for slot
        var waitStart = DateTime.UtcNow;
        var acquireTask = Task.Run(async () =>
        {
            await coordinator.AcquireSlotAsync();
            return DateTime.UtcNow;
        });

        // Wait a bit for the acquire to start waiting
        await Task.Delay(50);

        // Verify it's blocked
        acquireTask.IsCompleted.Should().BeFalse();

        // Release one slot
        coordinator.ReleaseSlot();

        // Now the waiting task should complete
        var completedAt = await acquireTask;
        var waitDuration = completedAt - waitStart;

        // Should have waited at least a bit
        waitDuration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(10));

        // Cleanup
        coordinator.ReleaseSlot();
        coordinator.ReleaseSlot();
        
        task1.Dispose();
        task2.Dispose();
    }

    [Fact]
    public async Task ActiveDownloads_ShouldTrackCorrectly()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 5);

        // Act - Acquire slots one by one
        for (int i = 1; i <= 3; i++)
        {
            await coordinator.AcquireSlotAsync();
            coordinator.ActiveDownloads.Should().Be(i);
            coordinator.AvailableSlots.Should().Be(5 - i);
        }

        // Release slots
        for (int i = 2; i >= 0; i--)
        {
            coordinator.ReleaseSlot();
            coordinator.ActiveDownloads.Should().Be(i);
            coordinator.AvailableSlots.Should().Be(5 - i);
        }
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task AcquireSlotAsync_WithCancelledToken_ShouldThrowImmediately()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 3);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => coordinator.AcquireSlotAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteDownloadAsync_WithCancelledToken_ShouldPropagate()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 3);
        var cts = new CancellationTokenSource();

        // Act
        async Task<int> Act()
        {
            return await coordinator.ExecuteDownloadAsync(async ct =>
            {
                await Task.Delay(100, ct);
                return 42;
            }, cts.Token);
        }

        // Start the task
        var task = Act();
        
        // Cancel after a short delay
        await Task.Delay(10);
        cts.Cancel();

        // Assert
        Func<Task<int>> act = () => task;
        await act.Should().ThrowAsync<OperationCanceledException>();

        // Slot should be released even after cancellation
        coordinator.ActiveDownloads.Should().Be(0);
        coordinator.AvailableSlots.Should().Be(3);
    }

    #endregion

    #region Resource Management Tests

    [Fact]
    public void Dispose_ShouldReleaseSemaphore()
    {
        // Arrange & Act - Just verify it doesn't throw
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 3);
        
        var act = () => coordinator.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task AcquireAfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 3);
        coordinator.Dispose();

        // Act
        var act = () => coordinator.AcquireSlotAsync();

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void ReleaseAfterDispose_ShouldNotThrow()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 3);
        coordinator.Dispose();

        // Act - Should not throw
        var act = () => coordinator.ReleaseSlot();

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentAcquireAndRelease_ShouldNotCorruptState()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 3);
        var tasks = new List<Task>();

        // Act - Many concurrent acquires and releases
        for (int i = 0; i < 100; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(async () =>
            {
                await coordinator.AcquireSlotAsync();
                await Task.Delay(10);
                coordinator.ReleaseSlot();
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        coordinator.ActiveDownloads.Should().Be(0);
        coordinator.AvailableSlots.Should().Be(3);
    }

    [Fact]
    public async Task RapidAcquireRelease_ShouldMaintainConsistency()
    {
        // Arrange
        var coordinator = new DownloadCoordinator(_loggerMock.Object, 5);
        var iterations = 50;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            await coordinator.AcquireSlotAsync();
            coordinator.ReleaseSlot();
        }

        // Assert
        coordinator.ActiveDownloads.Should().Be(0);
        coordinator.AvailableSlots.Should().Be(5);
    }

    #endregion
}