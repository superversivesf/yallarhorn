namespace Yallarhorn.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yallarhorn.Configuration;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Models;
using Yallarhorn.Services;

public class DownloadPipelineTests
{
    private readonly Mock<ILogger<DownloadPipeline>> _loggerMock;
    private readonly Mock<IYtDlpClient> _ytDlpClientMock;
    private readonly Mock<ITranscodeService> _transcodeServiceMock;
    private readonly Mock<IEpisodeRepository> _episodeRepositoryMock;
    private readonly Mock<IChannelRepository> _channelRepositoryMock;
    private readonly Mock<IDownloadCoordinator> _downloadCoordinatorMock;
    private readonly string _downloadDirectory;
    private readonly string _tempDirectory;

    public DownloadPipelineTests()
    {
        _loggerMock = new Mock<ILogger<DownloadPipeline>>();
        _ytDlpClientMock = new Mock<IYtDlpClient>();
        _transcodeServiceMock = new Mock<ITranscodeService>();
        _episodeRepositoryMock = new Mock<IEpisodeRepository>();
        _channelRepositoryMock = new Mock<IChannelRepository>();
        _downloadCoordinatorMock = new Mock<IDownloadCoordinator>();

        _downloadDirectory = Path.Combine(Path.GetTempPath(), "yallarhorn_test_downloads");
        _tempDirectory = Path.Combine(Path.GetTempPath(), "yallarhorn_test_temp");

        // Setup download coordinator to execute operations directly
        _downloadCoordinatorMock
            .Setup(d => d.ExecuteDownloadAsync(It.IsAny<Func<Task<PipelineResult>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (Func<Task<PipelineResult>> operation, CancellationToken _) => await operation());

        _downloadCoordinatorMock
            .Setup(d => d.ExecuteDownloadAsync(It.IsAny<Func<CancellationToken, Task<PipelineResult>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (Func<CancellationToken, Task<PipelineResult>> operation, CancellationToken ct) => await operation(ct));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange & Act
        var pipeline = new DownloadPipeline(
            _loggerMock.Object,
            _ytDlpClientMock.Object,
            _transcodeServiceMock.Object,
            _episodeRepositoryMock.Object,
            _channelRepositoryMock.Object,
            _downloadCoordinatorMock.Object,
            _downloadDirectory,
            _tempDirectory);

        // Assert
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldImplementIDownloadPipeline()
    {
        // Arrange & Act
        var pipeline = new DownloadPipeline(
            _loggerMock.Object,
            _ytDlpClientMock.Object,
            _transcodeServiceMock.Object,
            _episodeRepositoryMock.Object,
            _channelRepositoryMock.Object,
            _downloadCoordinatorMock.Object,
            _downloadDirectory,
            _tempDirectory);

        // Assert
        pipeline.Should().BeAssignableTo<IDownloadPipeline>();
    }

    #endregion

    #region ExecuteAsync Basic Flow Tests

    [Fact]
    public async Task ExecuteAsync_WithValidEpisode_ShouldCompleteSuccessfully()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel(feedType: FeedType.Audio);

        SetupEpisodeAndChannel(episode, channel);
        SetupSuccessfulDownload(episode);
        SetupSuccessfulTranscode(episode, channel);

        // Act
        var result = await pipeline.ExecuteAsync(episode.Id);

        // Assert
        result.Success.Should().BeTrue();
        result.EpisodeId.Should().Be(episode.Id);
        result.Error.Should().BeNull();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUpdateEpisodeStatus_ThroughPipeline()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel(feedType: FeedType.Audio);

        var statusUpdates = new List<EpisodeStatus>();
        SetupEpisodeAndChannel(episode, channel, onStatusUpdate: status => statusUpdates.Add(status));
        SetupSuccessfulDownload(episode);
        SetupSuccessfulTranscode(episode, channel);

        // Act
        await pipeline.ExecuteAsync(episode.Id);

        // Assert
        statusUpdates.Should().Contain(EpisodeStatus.Downloading);
        statusUpdates.Should().Contain(EpisodeStatus.Processing);
        statusUpdates.Should().Contain(EpisodeStatus.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellationToken_ShouldPassThrough()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel();
        var cts = new CancellationTokenSource();

        SetupEpisodeAndChannel(episode, channel);
        SetupSuccessfulDownload(episode);
        SetupSuccessfulTranscode(episode, channel);

        // Act
        var result = await pipeline.ExecuteAsync(episode.Id, cancellationToken: cts.Token);

        // Assert
        result.Success.Should().BeTrue();
    }

    #endregion

    #region Download Stage Tests

    [Fact]
    public async Task ExecuteAsync_ShouldDownloadVideo_WithCorrectUrl()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel(feedType: FeedType.Audio);

        SetupEpisodeAndChannel(episode, channel);
        SetupSuccessfulDownload(episode);
        SetupSuccessfulTranscode(episode, channel);

        string? capturedUrl = null;
        _ytDlpClientMock
            .Setup(y => y.DownloadVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, Action<DownloadProgress>?, CancellationToken>((url, _, _, _) => capturedUrl = url)
            .ReturnsAsync("/tmp/download/video123.mp4");

        // Act
        await pipeline.ExecuteAsync(episode.Id);

        // Assert
        capturedUrl.Should().Contain(episode.VideoId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDownloadFails_ShouldUpdateEpisodeStatusToFailed()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel();

        SetupEpisodeAndChannel(episode, channel);
        _ytDlpClientMock
            .Setup(y => y.DownloadVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new YtDlpException("Download failed", 1));

        // Act
        var result = await pipeline.ExecuteAsync(episode.Id);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Download failed");
        episode.Status.Should().Be(EpisodeStatus.Failed);
        episode.ErrorMessage.Should().Contain("Download failed");
    }

    [Fact]
    public async Task ExecuteAsync_WithProgressCallback_ShouldForwardProgress()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel();

        SetupEpisodeAndChannel(episode, channel);

        SetupSuccessfulTranscode(episode, channel);

        var progressReports = new List<PipelineProgress>();
        _ytDlpClientMock
            .Setup(y => y.DownloadVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, Action<DownloadProgress>?, CancellationToken>((_, _, progress, _) =>
            {
                if (progress != null)
                {
                    progress(new DownloadProgress { Status = "downloading", Progress = 50 });
                }
            })
            .ReturnsAsync("/tmp/download/video123.mp4");

        // Act
        var result = await pipeline.ExecuteAsync(
            episode.Id,
            progressCallback: p => progressReports.Add(p));

        // Assert
        result.Success.Should().BeTrue();
        progressReports.Should().NotBeEmpty();
    }

    #endregion

    #region Transcode Stage Tests

    [Fact]
    public async Task ExecuteAsync_WithAudioFeedType_ShouldTranscodeAudioOnly()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel(feedType: FeedType.Audio);

        SetupEpisodeAndChannel(episode, channel);
        SetupSuccessfulDownload(episode);
        SetupSuccessfulTranscode(episode, channel);

        // Act
        await pipeline.ExecuteAsync(episode.Id);

        // Assert
        _transcodeServiceMock.Verify(t => t.TranscodeAsync(
            It.IsAny<string>(),
            It.Is<Channel>(c => c.Id == channel.Id),
            It.Is<Episode>(e => e.Id == episode.Id),
            It.IsAny<Action<TranscodeProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithVideoFeedType_ShouldTranscodeVideo()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel(feedType: FeedType.Video);

        SetupEpisodeAndChannel(episode, channel);
        SetupSuccessfulDownload(episode);
        SetupSuccessfulTranscode(episode, channel);

        // Act
        await pipeline.ExecuteAsync(episode.Id);

        // Assert
        _transcodeServiceMock.Verify(t => t.TranscodeAsync(
            It.IsAny<string>(),
            It.IsAny<Channel>(),
            It.IsAny<Episode>(),
            It.IsAny<Action<TranscodeProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithBothFeedType_ShouldTranscodeBoth()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel(feedType: FeedType.Both);

        SetupEpisodeAndChannel(episode, channel);
        SetupSuccessfulDownload(episode);
        SetupSuccessfulTranscode(episode, channel);

        // Act
        await pipeline.ExecuteAsync(episode.Id);

        // Assert
        _transcodeServiceMock.Verify(t => t.TranscodeAsync(
            It.IsAny<string>(),
            It.IsAny<Channel>(),
            It.IsAny<Episode>(),
            It.IsAny<Action<TranscodeProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTranscodeFails_ShouldUpdateEpisodeStatusToFailed()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel();

        SetupEpisodeAndChannel(episode, channel);
        SetupSuccessfulDownload(episode);
        _transcodeServiceMock
            .Setup(t => t.TranscodeAsync(
                It.IsAny<string>(),
                It.IsAny<Channel>(),
                It.IsAny<Episode>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscodeServiceResult
            {
                Success = false,
                ErrorMessage = "Transcoding failed"
            });

        // Act
        var result = await pipeline.ExecuteAsync(episode.Id);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Transcoding failed");
        episode.Status.Should().Be(EpisodeStatus.Failed);
    }

    #endregion

    #region File Recording Tests

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ShouldUpdateEpisodeFilePaths()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel(feedType: FeedType.Both);

        SetupEpisodeAndChannel(episode, channel);
        SetupSuccessfulDownload(episode);

        _transcodeServiceMock
            .Setup(t => t.TranscodeAsync(
                It.IsAny<string>(),
                It.IsAny<Channel>(),
                It.IsAny<Episode>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Channel, Episode, Action<TranscodeProgress>?, CancellationToken>((_, _, ep, _, _) =>
            {
                ep.FilePathAudio = "channel123/audio/video123.mp3";
                ep.FilePathVideo = "channel123/video/video123.mp4";
                ep.FileSizeAudio = 1024 * 1024;
                ep.FileSizeVideo = 50 * 1024 * 1024;
            })
            .ReturnsAsync(new TranscodeServiceResult
            {
                Success = true,
                AudioPath = "/downloads/channel123/audio/video123.mp3",
                VideoPath = "/downloads/channel123/video/video123.mp4",
                AudioFileSize = 1024 * 1024,
                VideoFileSize = 50 * 1024 * 1024
            });

        // Act
        var result = await pipeline.ExecuteAsync(episode.Id);

        // Assert
        result.Success.Should().BeTrue();
        episode.FilePathAudio.Should().NotBeNull();
        episode.FilePathVideo.Should().NotBeNull();
        episode.FileSizeAudio.Should().Be(1024 * 1024);
        episode.FileSizeVideo.Should().Be(50L * 1024 * 1024);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ShouldUpdateDownloadedAt()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel(feedType: FeedType.Audio);

        SetupEpisodeAndChannel(episode, channel);
        SetupSuccessfulDownload(episode);
        SetupSuccessfulTranscode(episode, channel);

        // Act
        await pipeline.ExecuteAsync(episode.Id);

        // Assert
        episode.DownloadedAt.Should().NotBeNull();
        episode.DownloadedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Temp File Cleanup Tests

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ShouldCleanupTempFile()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel();

        // Create a temp file to simulate the download
        var tempFilePath = Path.Combine(_tempDirectory, "downloads", $"{episode.VideoId}.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(tempFilePath)!);
        await File.WriteAllTextAsync(tempFilePath, "test content");

        SetupEpisodeAndChannel(episode, channel);
        _ytDlpClientMock
            .Setup(y => y.DownloadVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tempFilePath);

        SetupSuccessfulTranscode(episode, channel);

        // Act
        var result = await pipeline.ExecuteAsync(episode.Id);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(tempFilePath).Should().BeFalse("temp file should be cleaned up");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTranscodeFails_ShouldCleanupTempFile()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel();

        // Create a temp file
        var tempFilePath = Path.Combine(_tempDirectory, "downloads", $"{episode.VideoId}.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(tempFilePath)!);
        await File.WriteAllTextAsync(tempFilePath, "test content");

        SetupEpisodeAndChannel(episode, channel);
        _ytDlpClientMock
            .Setup(y => y.DownloadVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tempFilePath);

        _transcodeServiceMock
            .Setup(t => t.TranscodeAsync(
                It.IsAny<string>(),
                It.IsAny<Channel>(),
                It.IsAny<Episode>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscodeServiceResult
            {
                Success = false,
                ErrorMessage = "Transcoding failed"
            });

        // Act
        var result = await pipeline.ExecuteAsync(episode.Id);

        // Assert
        result.Success.Should().BeFalse();
        File.Exists(tempFilePath).Should().BeFalse("temp file should be cleaned up even on failure");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExecuteAsync_WhenEpisodeNotFound_ShouldReturnError()
    {
        // Arrange
        var pipeline = CreatePipeline();

        _episodeRepositoryMock
            .Setup(e => e.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Episode?)null);

        // Act
        var result = await pipeline.ExecuteAsync("non-existent-id");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteAsync_WhenChannelNotFound_ShouldReturnError()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, _) = CreateEpisodeAndChannel();

        _episodeRepositoryMock
            .Setup(e => e.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _channelRepositoryMock
            .Setup(c => c.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Channel?)null);

        // Act
        var result = await pipeline.ExecuteAsync(episode.Id);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Channel not found");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnexpectedException_ShouldUpdateEpisodeStatusToFailed()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel();

        SetupEpisodeAndChannel(episode, channel);

        _ytDlpClientMock
            .Setup(y => y.DownloadVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        // Act
        var result = await pipeline.ExecuteAsync(episode.Id);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Unexpected error");
        episode.Status.Should().Be(EpisodeStatus.Failed);
        episode.ErrorMessage.Should().Contain("Unexpected error");
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel();

        SetupEpisodeAndChannel(episode, channel);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => pipeline.ExecuteAsync(episode.Id, cancellationToken: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassCancellationTokenToYtDlpClient()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel();
        var cts = new CancellationTokenSource();

        SetupEpisodeAndChannel(episode, channel);
        SetupSuccessfulDownload(episode);
        SetupSuccessfulTranscode(episode, channel);

        // Act
        await pipeline.ExecuteAsync(episode.Id, cancellationToken: cts.Token);

        // Assert
        _ytDlpClientMock.Verify(y => y.DownloadVideoAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Action<DownloadProgress>?>(),
            cts.Token), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassCancellationTokenToTranscodeService()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel();
        var cts = new CancellationTokenSource();

        SetupEpisodeAndChannel(episode, channel);
        SetupSuccessfulDownload(episode);
        SetupSuccessfulTranscode(episode, channel);

        // Act
        await pipeline.ExecuteAsync(episode.Id, cancellationToken: cts.Token);

        // Assert
        _transcodeServiceMock.Verify(t => t.TranscodeAsync(
            It.IsAny<string>(),
            It.IsAny<Channel>(),
            It.IsAny<Episode>(),
            It.IsAny<Action<TranscodeProgress>?>(),
            cts.Token), Times.Once);
    }

    #endregion

    #region PipelineResult Tests

    [Fact]
    public void PipelineResult_ShouldStoreAllProperties()
    {
        // Arrange & Act
        var result = new PipelineResult
        {
            Success = true,
            EpisodeId = "episode-123",
            DownloadPath = "/downloads/video.mp4",
            AudioPath = "/downloads/audio.mp3",
            VideoPath = "/downloads/video.mp4",
            Duration = TimeSpan.FromMinutes(5)
        };

        // Assert
        result.Success.Should().BeTrue();
        result.EpisodeId.Should().Be("episode-123");
        result.DownloadPath.Should().Be("/downloads/video.mp4");
        result.AudioPath.Should().Be("/downloads/audio.mp3");
        result.VideoPath.Should().Be("/downloads/video.mp4");
        result.Duration.Should().Be(TimeSpan.FromMinutes(5));
        result.Error.Should().BeNull();
    }

    [Fact]
    public void PipelineResult_WithFailure_ShouldIndicateFailure()
    {
        // Arrange & Act
        var result = new PipelineResult
        {
            Success = false,
            EpisodeId = "episode-123",
            Error = "Download failed",
            Duration = TimeSpan.FromSeconds(30)
        };

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Download failed");
    }

    #endregion

    #region Coordinator Integration Tests

    [Fact]
    public async Task ExecuteAsync_ShouldUseDownloadCoordinator()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel();

        SetupEpisodeAndChannel(episode, channel);
        SetupSuccessfulDownload(episode);
        SetupSuccessfulTranscode(episode, channel);

        // Act
        await pipeline.ExecuteAsync(episode.Id);

        // Assert
        _downloadCoordinatorMock.Verify(d => d.ExecuteDownloadAsync(
            It.IsAny<Func<CancellationToken, Task<PipelineResult>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Progress Stages Tests

    [Fact]
    public async Task ExecuteAsync_ShouldReportProgressStages()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var (episode, channel) = CreateEpisodeAndChannel();

        SetupEpisodeAndChannel(episode, channel);
        SetupSuccessfulDownload(episode);
        SetupSuccessfulTranscode(episode, channel);

        var stages = new List<PipelineStage>();
        PipelineProgress? lastProgress = null;

        // Act
        await pipeline.ExecuteAsync(
            episode.Id,
            progressCallback: p =>
            {
                stages.Add(p.Stage);
                lastProgress = p;
            });

        // Assert
        stages.Should().Contain(PipelineStage.Downloading);
        stages.Should().Contain(PipelineStage.Transcoding);
        stages.Should().Contain(PipelineStage.Completed);

        lastProgress.Should().NotBeNull();
        lastProgress!.Stage.Should().Be(PipelineStage.Completed);
    }

    #endregion

    #region Helper Methods

    private DownloadPipeline CreatePipeline()
    {
        return new DownloadPipeline(
            _loggerMock.Object,
            _ytDlpClientMock.Object,
            _transcodeServiceMock.Object,
            _episodeRepositoryMock.Object,
            _channelRepositoryMock.Object,
            _downloadCoordinatorMock.Object,
            _downloadDirectory,
            _tempDirectory);
    }

    private static (Episode episode, Channel channel) CreateEpisodeAndChannel(
        string? episodeId = null,
        string? channelId = null,
        string? videoId = null,
        FeedType feedType = FeedType.Audio)
    {
        var channel = new Channel
        {
            Id = channelId ?? "channel123",
            Url = "https://youtube.com/@testchannel",
            Title = "Test Channel",
            FeedType = feedType,
            EpisodeCountConfig = 50,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var episode = new Episode
        {
            Id = episodeId ?? "episode123",
            VideoId = videoId ?? "video123",
            ChannelId = channel.Id,
            Title = "Test Episode",
            Description = "Test Description",
            DurationSeconds = 300,
            Status = EpisodeStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        return (episode, channel);
    }

    private void SetupEpisodeAndChannel(
        Episode episode,
        Channel channel,
        Action<EpisodeStatus>? onStatusUpdate = null)
    {
        _episodeRepositoryMock
            .Setup(e => e.GetByIdAsync(episode.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(episode);

        _channelRepositoryMock
            .Setup(c => c.GetByIdAsync(episode.ChannelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);

        _episodeRepositoryMock
            .Setup(e => e.UpdateAsync(It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Callback<Episode, CancellationToken>((ep, _) =>
            {
                onStatusUpdate?.Invoke(ep.Status);
            })
            .Returns(Task.CompletedTask);
    }

    private void SetupSuccessfulDownload(Episode episode)
    {
        _ytDlpClientMock
            .Setup(y => y.DownloadVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<DownloadProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/downloads/video123.mp4");
    }

    private void SetupSuccessfulTranscode(Episode episode, Channel channel)
    {
        _transcodeServiceMock
            .Setup(t => t.TranscodeAsync(
                It.IsAny<string>(),
                It.IsAny<Channel>(),
                It.IsAny<Episode>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Channel, Episode, Action<TranscodeProgress>?, CancellationToken>((_, _, ep, _, _) =>
            {
                if (channel.FeedType is FeedType.Audio or FeedType.Both)
                {
                    ep.FilePathAudio = "channel123/audio/video123.mp3";
                    ep.FileSizeAudio = 1024 * 1024;
                }

                if (channel.FeedType is FeedType.Video or FeedType.Both)
                {
                    ep.FilePathVideo = "channel123/video/video123.mp4";
                    ep.FileSizeVideo = 50 * 1024 * 1024;
                }
            })
            .ReturnsAsync(new TranscodeServiceResult
            {
                Success = true,
                AudioPath = channel.FeedType is FeedType.Audio or FeedType.Both ? "/downloads/audio.mp3" : null,
                VideoPath = channel.FeedType is FeedType.Video or FeedType.Both ? "/downloads/video.mp4" : null,
                AudioFileSize = channel.FeedType is FeedType.Audio or FeedType.Both ? 1024 * 1024 : null,
                VideoFileSize = channel.FeedType is FeedType.Video or FeedType.Both ? 50L * 1024 * 1024 : null
            });
    }

    #endregion
}