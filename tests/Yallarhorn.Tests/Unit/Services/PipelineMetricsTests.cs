namespace Yallarhorn.Tests.Unit.Services;

using FluentAssertions;
using Xunit;
using Yallarhorn.Services;
using Yallarhorn.Models;

public class PipelineMetricsTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeWithZeroCounts()
    {
        // Act
        var metrics = new PipelineMetrics();

        // Assert
        var stats = metrics.GetStats();
        stats.DownloadsStarted.Should().Be(0);
        stats.DownloadsCompleted.Should().Be(0);
        stats.DownloadsFailed.Should().Be(0);
        stats.TotalBytesDownloaded.Should().Be(0);
        stats.AverageDownloadDuration.Should().BeNull();
    }

    #endregion

    #region RecordDownloadStarted Tests

    [Fact]
    public void RecordDownloadStarted_ShouldIncrementStartedCount()
    {
        // Arrange
        var metrics = new PipelineMetrics();

        // Act
        metrics.RecordDownloadStarted("episode-1", "channel-1");

        // Assert
        var stats = metrics.GetStats();
        stats.DownloadsStarted.Should().Be(1);
    }

    [Fact]
    public void RecordDownloadStarted_MultipleCalls_ShouldIncrementCorrectly()
    {
        // Arrange
        var metrics = new PipelineMetrics();

        // Act
        metrics.RecordDownloadStarted("episode-1", "channel-1");
        metrics.RecordDownloadStarted("episode-2", "channel-1");
        metrics.RecordDownloadStarted("episode-3", "channel-2");

        // Assert
        var stats = metrics.GetStats();
        stats.DownloadsStarted.Should().Be(3);
    }

    #endregion

    #region RecordDownloadCompleted Tests

    [Fact]
    public void RecordDownloadCompleted_ShouldIncrementCompletedCount()
    {
        // Arrange
        var metrics = new PipelineMetrics();

        // Act
        metrics.RecordDownloadCompleted("episode-1", TimeSpan.FromSeconds(30), 1024 * 1024);

        // Assert
        var stats = metrics.GetStats();
        stats.DownloadsCompleted.Should().Be(1);
    }

    [Fact]
    public void RecordDownloadCompleted_ShouldTrackTotalBytes()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        var bytes1 = 1024L * 1024L; // 1 MB
        var bytes2 = 2L * 1024L * 1024L; // 2 MB

        // Act
        metrics.RecordDownloadCompleted("episode-1", TimeSpan.FromSeconds(30), bytes1);
        metrics.RecordDownloadCompleted("episode-2", TimeSpan.FromSeconds(45), bytes2);

        // Assert
        var stats = metrics.GetStats();
        stats.TotalBytesDownloaded.Should().Be(bytes1 + bytes2);
    }

    [Fact]
    public void RecordDownloadCompleted_ShouldCalculateAverageDuration()
    {
        // Arrange
        var metrics = new PipelineMetrics();

        // Act
        metrics.RecordDownloadCompleted("episode-1", TimeSpan.FromSeconds(30), 1024);
        metrics.RecordDownloadCompleted("episode-2", TimeSpan.FromSeconds(60), 1024);
        metrics.RecordDownloadCompleted("episode-3", TimeSpan.FromSeconds(90), 1024);

        // Assert
        var stats = metrics.GetStats();
        stats.AverageDownloadDuration.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void RecordDownloadCompleted_SingleCompletion_ShouldReturnCorrectAverage()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        var duration = TimeSpan.FromSeconds(45);

        // Act
        metrics.RecordDownloadCompleted("episode-1", duration, 1024);

        // Assert
        var stats = metrics.GetStats();
        stats.AverageDownloadDuration.Should().Be(duration);
    }

    #endregion

    #region RecordDownloadFailed Tests

    [Fact]
    public void RecordDownloadFailed_ShouldIncrementFailedCount()
    {
        // Arrange
        var metrics = new PipelineMetrics();

        // Act
        metrics.RecordDownloadFailed("episode-1", ErrorCategory.NetworkError);

        // Assert
        var stats = metrics.GetStats();
        stats.DownloadsFailed.Should().Be(1);
    }

    [Fact]
    public void RecordDownloadFailed_ShouldTrackErrorByCategory()
    {
        // Arrange
        var metrics = new PipelineMetrics();

        // Act
        metrics.RecordDownloadFailed("episode-1", ErrorCategory.NetworkError);
        metrics.RecordDownloadFailed("episode-2", ErrorCategory.NetworkError);
        metrics.RecordDownloadFailed("episode-3", ErrorCategory.VideoNotFound);
        metrics.RecordDownloadFailed("episode-4", ErrorCategory.VideoPrivate);

        // Assert
        var stats = metrics.GetStats();
        stats.ErrorCounts.Should().ContainKey(ErrorCategory.NetworkError.ToString());
        stats.ErrorCounts[ErrorCategory.NetworkError.ToString()].Should().Be(2);
        stats.ErrorCounts[ErrorCategory.VideoNotFound.ToString()].Should().Be(1);
        stats.ErrorCounts[ErrorCategory.VideoPrivate.ToString()].Should().Be(1);
    }

    [Fact]
    public void RecordDownloadFailed_AllErrorCategories_ShouldBeTracked()
    {
        // Arrange
        var metrics = new PipelineMetrics();

        // Act
        metrics.RecordDownloadFailed("episode-1", ErrorCategory.NetworkError);
        metrics.RecordDownloadFailed("episode-2", ErrorCategory.VideoNotFound);
        metrics.RecordDownloadFailed("episode-3", ErrorCategory.VideoPrivate);
        metrics.RecordDownloadFailed("episode-4", ErrorCategory.TranscodeError);
        metrics.RecordDownloadFailed("episode-5", ErrorCategory.Cancelled);
        metrics.RecordDownloadFailed("episode-6", ErrorCategory.Unknown);

        // Assert
        var stats = metrics.GetStats();
        stats.ErrorCounts.Should().HaveCount(6);
    }

    #endregion

    #region RecordTranscodeCompleted Tests

    [Fact]
    public void RecordTranscodeCompleted_ShouldTrackAudioTranscodes()
    {
        // Arrange
        var metrics = new PipelineMetrics();

        // Act
        metrics.RecordTranscodeCompleted("episode-1", TranscodeFormat.Audio, TimeSpan.FromSeconds(10));

        // Assert
        var stats = metrics.GetStats();
        stats.TranscodeCounts.Should().ContainKey(TranscodeFormat.Audio.ToString());
        stats.TranscodeCounts[TranscodeFormat.Audio.ToString()].Should().Be(1);
    }

    [Fact]
    public void RecordTranscodeCompleted_ShouldTrackVideoTranscodes()
    {
        // Arrange
        var metrics = new PipelineMetrics();

        // Act
        metrics.RecordTranscodeCompleted("episode-1", TranscodeFormat.Video, TimeSpan.FromSeconds(30));

        // Assert
        var stats = metrics.GetStats();
        stats.TranscodeCounts.Should().ContainKey(TranscodeFormat.Video.ToString());
        stats.TranscodeCounts[TranscodeFormat.Video.ToString()].Should().Be(1);
    }

    [Fact]
    public void RecordTranscodeCompleted_MultipleTranscodes_ShouldTrackCorrectly()
    {
        // Arrange
        var metrics = new PipelineMetrics();

        // Act
        metrics.RecordTranscodeCompleted("episode-1", TranscodeFormat.Audio, TimeSpan.FromSeconds(10));
        metrics.RecordTranscodeCompleted("episode-1", TranscodeFormat.Video, TimeSpan.FromSeconds(30));
        metrics.RecordTranscodeCompleted("episode-2", TranscodeFormat.Audio, TimeSpan.FromSeconds(15));
        metrics.RecordTranscodeCompleted("episode-3", TranscodeFormat.Video, TimeSpan.FromSeconds(45));

        // Assert
        var stats = metrics.GetStats();
        stats.TranscodeCounts[TranscodeFormat.Audio.ToString()].Should().Be(2);
        stats.TranscodeCounts[TranscodeFormat.Video.ToString()].Should().Be(2);
    }

    [Fact]
    public void RecordTranscodeCompleted_ShouldTrackDurationByFormat()
    {
        // Arrange
        var metrics = new PipelineMetrics();

        // Act
        metrics.RecordTranscodeCompleted("episode-1", TranscodeFormat.Audio, TimeSpan.FromSeconds(10));
        metrics.RecordTranscodeCompleted("episode-2", TranscodeFormat.Audio, TimeSpan.FromSeconds(20));

        // Assert
        var stats = metrics.GetStats();
        stats.AverageTranscodeDurations.Should().ContainKey(TranscodeFormat.Audio.ToString());
        stats.AverageTranscodeDurations[TranscodeFormat.Audio.ToString()].Should().Be(TimeSpan.FromSeconds(15));
    }

    #endregion

    #region QueueDepth Tests

    [Fact]
    public void QueueDepth_Initially_ShouldBeZero()
    {
        // Arrange
        var metrics = new PipelineMetrics();

        // Act
        var stats = metrics.GetStats();

        // Assert
        stats.QueueDepth.Pending.Should().Be(0);
        stats.QueueDepth.InProgress.Should().Be(0);
        stats.QueueDepth.Retrying.Should().Be(0);
    }

    [Fact]
    public void UpdateQueueDepth_ShouldReflectInStats()
    {
        // Arrange
        var metrics = new PipelineMetrics();

        // Act
        metrics.UpdateQueueDepth(pending: 5, inProgress: 2, retrying: 1);
        var stats = metrics.GetStats();

        // Assert
        stats.QueueDepth.Pending.Should().Be(5);
        stats.QueueDepth.InProgress.Should().Be(2);
        stats.QueueDepth.Retrying.Should().Be(1);
    }

    [Fact]
    public void UpdateQueueDepth_MultipleUpdates_ShouldReflectLatestValue()
    {
        // Arrange
        var metrics = new PipelineMetrics();

        // Act
        metrics.UpdateQueueDepth(pending: 5, inProgress: 2, retrying: 1);
        metrics.UpdateQueueDepth(pending: 10, inProgress: 3, retrying: 2);
        var stats = metrics.GetStats();

        // Assert
        stats.QueueDepth.Pending.Should().Be(10);
        stats.QueueDepth.InProgress.Should().Be(3);
        stats.QueueDepth.Retrying.Should().Be(2);
    }

    #endregion

    #region GetStats Tests

    [Fact]
    public void GetStats_ShouldReturnCompleteStats()
    {
        // Arrange
        var metrics = new PipelineMetrics();

        // Act
        metrics.RecordDownloadStarted("episode-1", "channel-1");
        metrics.RecordDownloadStarted("episode-2", "channel-1");
        metrics.RecordDownloadCompleted("episode-1", TimeSpan.FromSeconds(30), 1024 * 1024);
        metrics.RecordDownloadFailed("episode-3", ErrorCategory.NetworkError);
        metrics.RecordTranscodeCompleted("episode-1", TranscodeFormat.Audio, TimeSpan.FromSeconds(10));
        metrics.UpdateQueueDepth(pending: 5, inProgress: 2, retrying: 1);

        var stats = metrics.GetStats();

        // Assert
        stats.DownloadsStarted.Should().Be(2);
        stats.DownloadsCompleted.Should().Be(1);
        stats.DownloadsFailed.Should().Be(1);
        stats.TotalBytesDownloaded.Should().Be(1024 * 1024);
        stats.AverageDownloadDuration.Should().Be(TimeSpan.FromSeconds(30));
        stats.TranscodeCounts.Should().ContainKey(TranscodeFormat.Audio.ToString());
        stats.ErrorCounts.Should().ContainKey(ErrorCategory.NetworkError.ToString());
        stats.QueueDepth.Pending.Should().Be(5);
        stats.QueueDepth.InProgress.Should().Be(2);
        stats.QueueDepth.Retrying.Should().Be(1);
    }

    [Fact]
    public void GetStats_AfterNoOperations_ShouldReturnEmptyStats()
    {
        // Arrange
        var metrics = new PipelineMetrics();

        // Act
        var stats = metrics.GetStats();

        // Assert
        stats.DownloadsStarted.Should().Be(0);
        stats.DownloadsCompleted.Should().Be(0);
        stats.DownloadsFailed.Should().Be(0);
        stats.TotalBytesDownloaded.Should().Be(0);
        stats.AverageDownloadDuration.Should().BeNull();
        stats.TranscodeCounts.Should().BeEmpty();
        stats.ErrorCounts.Should().BeEmpty();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void RecordDownloadStarted_ConcurrentCalls_ShouldCountCorrectly()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        var iterations = 100;

        // Act
        Parallel.For(0, iterations, i =>
        {
            metrics.RecordDownloadStarted($"episode-{i}", $"channel-{i % 10}");
        });

        // Assert
        var stats = metrics.GetStats();
        stats.DownloadsStarted.Should().Be(iterations);
    }

    [Fact]
    public void RecordDownloadCompleted_ConcurrentCalls_ShouldCountCorrectly()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        var iterations = 100;

        // Act
        Parallel.For(0, iterations, i =>
        {
            metrics.RecordDownloadCompleted($"episode-{i}", TimeSpan.FromSeconds(i), 1024 * i);
        });

        // Assert
        var stats = metrics.GetStats();
        stats.DownloadsCompleted.Should().Be(iterations);
        stats.TotalBytesDownloaded.Should().Be(1024L * (iterations - 1) * iterations / 2);
    }

    [Fact]
    public void RecordDownloadFailed_ConcurrentCalls_ShouldCountCorrectly()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        var iterations = 100;
        var categories = Enum.GetValues<ErrorCategory>();

        // Act
        Parallel.For(0, iterations, i =>
        {
            var category = categories[i % categories.Length];
            metrics.RecordDownloadFailed($"episode-{i}", category);
        });

        // Assert
        var stats = metrics.GetStats();
        stats.DownloadsFailed.Should().Be(iterations);
    }

    [Fact]
    public async Task MixedOperations_ConcurrentCalls_ShouldMaintainConsistency()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        var startedCount = 0;
        var completedCount = 0;
        var failedCount = 0;
        var lockObj = new object();

        // Act
        var tasks = new List<Task>();

        for (int i = 0; i < 50; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(() =>
            {
                metrics.RecordDownloadStarted($"episode-{idx}", $"channel-{idx % 5}");
                lock (lockObj) { startedCount++; }
            }));
        }

        for (int i = 0; i < 30; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(() =>
            {
                metrics.RecordDownloadCompleted($"episode-{idx}", TimeSpan.FromSeconds(idx), 1024L * idx);
                lock (lockObj) { completedCount++; }
            }));
        }

        for (int i = 0; i < 20; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(() =>
            {
                metrics.RecordDownloadFailed($"episode-{idx}", ErrorCategory.NetworkError);
                lock (lockObj) { failedCount++; }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var stats = metrics.GetStats();
        stats.DownloadsStarted.Should().Be(startedCount);
        stats.DownloadsCompleted.Should().Be(completedCount);
        stats.DownloadsFailed.Should().Be(failedCount);
    }

    [Fact]
    public async Task GetStats_ConcurrentCalls_ShouldReturnConsistentSnapshot()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        var exceptions = new List<Exception>();
        var lockObj = new object();

        // Act - Concurrent reads and writes
        var writeTask = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                metrics.RecordDownloadStarted($"episode-{i}", "channel-1");
                metrics.RecordDownloadCompleted($"episode-{i}", TimeSpan.FromSeconds(1), 1024);
            }
        });

        var readTasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 50; i++)
            {
                try
                {
                    var stats = metrics.GetStats();
                    // Stats should be internally consistent
                    stats.DownloadsStarted.Should().BeGreaterOrEqualTo(0);
                    stats.DownloadsCompleted.Should().BeGreaterOrEqualTo(0);
                    stats.DownloadsCompleted.Should().BeLessOrEqualTo(stats.DownloadsStarted);
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        exceptions.Add(ex);
                    }
                }
            }
        }));

        await Task.WhenAll([writeTask, .. readTasks]);

        // Assert
        exceptions.Should().BeEmpty();
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ShouldClearAllMetrics()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        metrics.RecordDownloadStarted("episode-1", "channel-1");
        metrics.RecordDownloadCompleted("episode-1", TimeSpan.FromSeconds(30), 1024 * 1024);
        metrics.RecordDownloadFailed("episode-2", ErrorCategory.NetworkError);
        metrics.RecordTranscodeCompleted("episode-1", TranscodeFormat.Audio, TimeSpan.FromSeconds(10));
        metrics.UpdateQueueDepth(5, 2, 1);

        // Act
        metrics.Reset();
        var stats = metrics.GetStats();

        // Assert
        stats.DownloadsStarted.Should().Be(0);
        stats.DownloadsCompleted.Should().Be(0);
        stats.DownloadsFailed.Should().Be(0);
        stats.TotalBytesDownloaded.Should().Be(0);
        stats.AverageDownloadDuration.Should().BeNull();
        stats.TranscodeCounts.Should().BeEmpty();
        stats.ErrorCounts.Should().BeEmpty();
        stats.QueueDepth.Pending.Should().Be(0);
        stats.QueueDepth.InProgress.Should().Be(0);
        stats.QueueDepth.Retrying.Should().Be(0);
    }

    #endregion
}