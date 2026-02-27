namespace Yallarhorn.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yallarhorn.Configuration;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Models;
using Yallarhorn.Services;

public class TranscodeServiceTests
{
    private readonly Mock<ILogger<TranscodeService>> _loggerMock;
    private readonly Mock<IFfmpegClient> _ffmpegClientMock;
    private readonly TranscodeOptions _defaultOptions;
    private readonly string _defaultDownloadDir;

    public TranscodeServiceTests()
    {
        _loggerMock = new Mock<ILogger<TranscodeService>>();
        _ffmpegClientMock = new Mock<IFfmpegClient>();
        _defaultOptions = new TranscodeOptions
        {
            AudioFormat = "mp3",
            AudioBitrate = "192k",
            AudioSampleRate = 44100,
            VideoFormat = "mp4",
            VideoCodec = "h264",
            VideoQuality = 23,
            Threads = 4,
            KeepOriginal = false
        };
        _defaultDownloadDir = Path.Combine(Path.GetTempPath(), "yallarhorn_test");
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange & Act
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldImplementITranscodeService()
    {
        // Arrange & Act
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        // Assert
        service.Should().BeAssignableTo<ITranscodeService>();
    }

    #endregion

    #region TranscodeAsync Audio FeedType Tests

    [Fact]
    public async Task TranscodeAsync_WithAudioFeedType_ShouldOnlyTranscodeAudio()
    {
        // Arrange
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        var inputPath = "/tmp/downloads/test-channel/video123.mp4";
        var channel = CreateChannel(feedType: FeedType.Audio);
        var episode = CreateEpisode(channel);

        string? capturedOutputPath = null;
        _ffmpegClientMock
            .Setup(f => f.TranscodeAudioAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AudioTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, AudioTranscodeSettings?, Action<TranscodeProgress>?, CancellationToken>(
                (_, output, _, _, _) => capturedOutputPath = output)
            .ReturnsAsync((string _, string output, AudioTranscodeSettings? _, Action<TranscodeProgress>? _, CancellationToken _) =>
                new TranscodeResult
                {
                    Success = true,
                    ExitCode = 0,
                    Duration = TimeSpan.FromSeconds(10),
                    OutputPath = output,
                    OutputFileSize = 1024 * 1024
                });

        // Act
        var result = await service.TranscodeAsync(inputPath, channel, episode);

        // Assert
        result.Success.Should().BeTrue();
        result.AudioPath.Should().NotBeNull();
        result.VideoPath.Should().BeNull();
        result.AudioFileSize.Should().Be(1024 * 1024);
        result.VideoFileSize.Should().BeNull();

        _ffmpegClientMock.Verify(f => f.TranscodeAudioAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<AudioTranscodeSettings?>(),
            It.IsAny<Action<TranscodeProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _ffmpegClientMock.Verify(f => f.TranscodeVideoAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<VideoTranscodeSettings?>(),
            It.IsAny<Action<TranscodeProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranscodeAsync_WithAudioFeedType_ShouldUpdateEpisodeAudioPath()
    {
        // Arrange
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        var inputPath = "/tmp/downloads/test-channel/video123.mp4";
        var channel = CreateChannel(feedType: FeedType.Audio);
        var episode = CreateEpisode(channel);

        _ffmpegClientMock
            .Setup(f => f.TranscodeAudioAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AudioTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string output, AudioTranscodeSettings? _, Action<TranscodeProgress>? _, CancellationToken _) =>
                new TranscodeResult
                {
                    Success = true,
                    ExitCode = 0,
                    Duration = TimeSpan.FromSeconds(10),
                    OutputPath = output,
                    OutputFileSize = 1024 * 1024
                });

        // Act
        await service.TranscodeAsync(inputPath, channel, episode);

        // Assert
        episode.FilePathAudio.Should().NotBeNull();
        episode.FilePathAudio.Should().Contain("audio");
        episode.FilePathAudio.Should().Contain("video123.mp3");
        episode.FileSizeAudio.Should().Be(1024 * 1024);
        episode.FilePathVideo.Should().BeNull();
    }

    #endregion

    #region TranscodeAsync Video FeedType Tests

    [Fact]
    public async Task TranscodeAsync_WithVideoFeedType_ShouldOnlyTranscodeVideo()
    {
        // Arrange
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        var inputPath = "/tmp/downloads/test-channel/video123.mp4";
        var channel = CreateChannel(feedType: FeedType.Video);
        var episode = CreateEpisode(channel);

        _ffmpegClientMock
            .Setup(f => f.TranscodeVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<VideoTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string output, VideoTranscodeSettings? _, Action<TranscodeProgress>? _, CancellationToken _) =>
                new TranscodeResult
                {
                    Success = true,
                    ExitCode = 0,
                    Duration = TimeSpan.FromSeconds(30),
                    OutputPath = output,
                    OutputFileSize = 50 * 1024 * 1024
                });

        // Act
        var result = await service.TranscodeAsync(inputPath, channel, episode);

        // Assert
        result.Success.Should().BeTrue();
        result.AudioPath.Should().BeNull();
        result.VideoPath.Should().NotBeNull();
        result.AudioFileSize.Should().BeNull();
        result.VideoFileSize.Should().Be(50L * 1024 * 1024);

        _ffmpegClientMock.Verify(f => f.TranscodeVideoAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<VideoTranscodeSettings?>(),
            It.IsAny<Action<TranscodeProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _ffmpegClientMock.Verify(f => f.TranscodeAudioAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<AudioTranscodeSettings?>(),
            It.IsAny<Action<TranscodeProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranscodeAsync_WithVideoFeedType_ShouldUpdateEpisodeVideoPath()
    {
        // Arrange
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        var inputPath = "/tmp/downloads/test-channel/video123.mp4";
        var channel = CreateChannel(feedType: FeedType.Video);
        var episode = CreateEpisode(channel);

        _ffmpegClientMock
            .Setup(f => f.TranscodeVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<VideoTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string output, VideoTranscodeSettings? _, Action<TranscodeProgress>? _, CancellationToken _) =>
                new TranscodeResult
                {
                    Success = true,
                    ExitCode = 0,
                    Duration = TimeSpan.FromSeconds(30),
                    OutputPath = output,
                    OutputFileSize = 50 * 1024 * 1024
                });

        // Act
        await service.TranscodeAsync(inputPath, channel, episode);

        // Assert
        episode.FilePathVideo.Should().NotBeNull();
        episode.FilePathVideo.Should().Contain("video");
        episode.FilePathVideo.Should().Contain("video123.mp4");
        episode.FileSizeVideo.Should().Be(50L * 1024 * 1024);
        episode.FilePathAudio.Should().BeNull();
    }

    #endregion

    #region TranscodeAsync Both FeedType Tests

    [Fact]
    public async Task TranscodeAsync_WithBothFeedType_ShouldTranscodeAudioAndVideo()
    {
        // Arrange
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        var inputPath = "/tmp/downloads/test-channel/video123.mp4";
        var channel = CreateChannel(feedType: FeedType.Both);
        var episode = CreateEpisode(channel);

        _ffmpegClientMock
            .Setup(f => f.TranscodeAudioAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AudioTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string output, AudioTranscodeSettings? _, Action<TranscodeProgress>? _, CancellationToken _) =>
                new TranscodeResult
                {
                    Success = true,
                    ExitCode = 0,
                    Duration = TimeSpan.FromSeconds(10),
                    OutputPath = output,
                    OutputFileSize = 1024 * 1024
                });

        _ffmpegClientMock
            .Setup(f => f.TranscodeVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<VideoTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string output, VideoTranscodeSettings? _, Action<TranscodeProgress>? _, CancellationToken _) =>
                new TranscodeResult
                {
                    Success = true,
                    ExitCode = 0,
                    Duration = TimeSpan.FromSeconds(30),
                    OutputPath = output,
                    OutputFileSize = 50 * 1024 * 1024
                });

        // Act
        var result = await service.TranscodeAsync(inputPath, channel, episode);

        // Assert
        result.Success.Should().BeTrue();
        result.AudioPath.Should().NotBeNull();
        result.VideoPath.Should().NotBeNull();
        result.AudioFileSize.Should().Be(1024L * 1024);
        result.VideoFileSize.Should().Be(50L * 1024 * 1024);

        _ffmpegClientMock.Verify(f => f.TranscodeAudioAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<AudioTranscodeSettings?>(),
            It.IsAny<Action<TranscodeProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _ffmpegClientMock.Verify(f => f.TranscodeVideoAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<VideoTranscodeSettings?>(),
            It.IsAny<Action<TranscodeProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TranscodeAsync_WithBothFeedType_ShouldUpdateBothEpisodePaths()
    {
        // Arrange
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        var inputPath = "/tmp/downloads/test-channel/video123.mp4";
        var channel = CreateChannel(feedType: FeedType.Both);
        var episode = CreateEpisode(channel);

        _ffmpegClientMock
            .Setup(f => f.TranscodeAudioAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AudioTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string output, AudioTranscodeSettings? _, Action<TranscodeProgress>? _, CancellationToken _) =>
                new TranscodeResult
                {
                    Success = true,
                    ExitCode = 0,
                    Duration = TimeSpan.FromSeconds(10),
                    OutputPath = output,
                    OutputFileSize = 1024 * 1024
                });

        _ffmpegClientMock
            .Setup(f => f.TranscodeVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<VideoTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string output, VideoTranscodeSettings? _, Action<TranscodeProgress>? _, CancellationToken _) =>
                new TranscodeResult
                {
                    Success = true,
                    ExitCode = 0,
                    Duration = TimeSpan.FromSeconds(30),
                    OutputPath = output,
                    OutputFileSize = 50 * 1024 * 1024
                });

        // Act
        await service.TranscodeAsync(inputPath, channel, episode);

        // Assert
        episode.FilePathAudio.Should().NotBeNull();
        episode.FilePathAudio.Should().Contain("audio");
        episode.FileSizeAudio.Should().Be(1024L * 1024);

        episode.FilePathVideo.Should().NotBeNull();
        episode.FilePathVideo.Should().Contain("video");
        episode.FileSizeVideo.Should().Be(50L * 1024 * 1024);
    }

    #endregion

    #region Output Path Generation Tests

    [Fact]
    public async Task TranscodeAsync_ShouldGenerateCorrectAudioPath()
    {
        // Arrange
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        var inputPath = "/tmp/input.mp4";
        var channel = CreateChannel(id: "channel-abc-123", feedType: FeedType.Audio);
        var episode = CreateEpisode(channel, videoId: "video-xyz-789");

        string? capturedOutputPath = null;
        _ffmpegClientMock
            .Setup(f => f.TranscodeAudioAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AudioTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, AudioTranscodeSettings?, Action<TranscodeProgress>?, CancellationToken>(
                (_, output, _, _, _) => capturedOutputPath = output)
            .ReturnsAsync(new TranscodeResult
            {
                Success = true,
                ExitCode = 0,
                Duration = TimeSpan.FromSeconds(10),
                OutputFileSize = 1024
            });

        // Act
        await service.TranscodeAsync(inputPath, channel, episode);

        // Assert
        capturedOutputPath.Should().NotBeNull();
        capturedOutputPath.Should().Contain("channel-abc-123");
        capturedOutputPath.Should().Contain("audio");
        capturedOutputPath.Should().Contain("video-xyz-789.mp3");
    }

    [Fact]
    public async Task TranscodeAsync_ShouldGenerateCorrectVideoPath()
    {
        // Arrange
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        var inputPath = "/tmp/input.mp4";
        var channel = CreateChannel(id: "channel-abc-123", feedType: FeedType.Video);
        var episode = CreateEpisode(channel, videoId: "video-xyz-789");

        string? capturedOutputPath = null;
        _ffmpegClientMock
            .Setup(f => f.TranscodeVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<VideoTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, VideoTranscodeSettings?, Action<TranscodeProgress>?, CancellationToken>(
                (_, output, _, _, _) => capturedOutputPath = output)
            .ReturnsAsync(new TranscodeResult
            {
                Success = true,
                ExitCode = 0,
                Duration = TimeSpan.FromSeconds(30),
                OutputFileSize = 1024
            });

        // Act
        await service.TranscodeAsync(inputPath, channel, episode);

        // Assert
        capturedOutputPath.Should().NotBeNull();
        capturedOutputPath.Should().Contain("channel-abc-123");
        capturedOutputPath.Should().Contain("video");
        capturedOutputPath.Should().Contain("video-xyz-789.mp4");
    }

    [Fact]
    public async Task TranscodeAsync_WithM4AFormat_ShouldGenerateM4APath()
    {
        // Arrange
        var m4aOptions = new TranscodeOptions
        {
            AudioFormat = "m4a",
            AudioBitrate = "192k",
            AudioSampleRate = 44100,
            VideoFormat = "mp4",
            VideoCodec = "h264",
            VideoQuality = 23,
            Threads = 4,
            KeepOriginal = false
        };

        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            m4aOptions,
            _defaultDownloadDir);

        var inputPath = "/tmp/input.mp4";
        var channel = CreateChannel(feedType: FeedType.Audio);
        var episode = CreateEpisode(channel);

        string? capturedOutputPath = null;
        _ffmpegClientMock
            .Setup(f => f.TranscodeAudioAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AudioTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, AudioTranscodeSettings?, Action<TranscodeProgress>?, CancellationToken>(
                (_, output, _, _, _) => capturedOutputPath = output)
            .ReturnsAsync(new TranscodeResult
            {
                Success = true,
                ExitCode = 0,
                Duration = TimeSpan.FromSeconds(10),
                OutputFileSize = 1024
            });

        // Act
        await service.TranscodeAsync(inputPath, channel, episode);

        // Assert
        capturedOutputPath.Should().NotBeNull();
        capturedOutputPath.Should().EndWith(".m4a");
    }

    #endregion

    #region TranscodeSettings Mapping Tests

    [Fact]
    public async Task TranscodeAsync_ShouldUseCorrectAudioSettings()
    {
        // Arrange
        var customOptions = new TranscodeOptions
        {
            AudioFormat = "aac",
            AudioBitrate = "256k",
            AudioSampleRate = 48000,
            VideoFormat = "mp4",
            VideoCodec = "h264",
            VideoQuality = 20,
            Threads = 8,
            KeepOriginal = false
        };

        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            customOptions,
            _defaultDownloadDir);

        var channel = CreateChannel(feedType: FeedType.Audio);
        var episode = CreateEpisode(channel);

        AudioTranscodeSettings? capturedSettings = null;
        _ffmpegClientMock
            .Setup(f => f.TranscodeAudioAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AudioTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, AudioTranscodeSettings?, Action<TranscodeProgress>?, CancellationToken>(
                (_, _, settings, _, _) => capturedSettings = settings)
            .ReturnsAsync(new TranscodeResult
            {
                Success = true,
                ExitCode = 0,
                Duration = TimeSpan.FromSeconds(10),
                OutputFileSize = 1024
            });

        // Act
        await service.TranscodeAsync("/tmp/input.mp4", channel, episode);

        // Assert
        capturedSettings.Should().NotBeNull();
        capturedSettings!.Format.Should().Be("aac");
        capturedSettings.Bitrate.Should().Be("256k");
        capturedSettings.SampleRate.Should().Be(48000);
    }

    [Fact]
    public async Task TranscodeAsync_ShouldUseCorrectVideoSettings()
    {
        // Arrange
        var customOptions = new TranscodeOptions
        {
            AudioFormat = "mp3",
            AudioBitrate = "192k",
            AudioSampleRate = 44100,
            VideoFormat = "mp4",
            VideoCodec = "h265",
            VideoQuality = 20,
            Threads = 8,
            KeepOriginal = false
        };

        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            customOptions,
            _defaultDownloadDir);

        var channel = CreateChannel(feedType: FeedType.Video);
        var episode = CreateEpisode(channel);

        VideoTranscodeSettings? capturedSettings = null;
        _ffmpegClientMock
            .Setup(f => f.TranscodeVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<VideoTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, VideoTranscodeSettings?, Action<TranscodeProgress>?, CancellationToken>(
                (_, _, settings, _, _) => capturedSettings = settings)
            .ReturnsAsync(new TranscodeResult
            {
                Success = true,
                ExitCode = 0,
                Duration = TimeSpan.FromSeconds(30),
                OutputFileSize = 1024
            });

        // Act
        await service.TranscodeAsync("/tmp/input.mp4", channel, episode);

        // Assert
        capturedSettings.Should().NotBeNull();
        capturedSettings!.VideoCodec.Should().Be("libx265");
        capturedSettings.Quality.Should().Be(20);
        capturedSettings.AudioBitrate.Should().Be("192k");
        capturedSettings.AudioSampleRate.Should().Be(44100);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task TranscodeAsync_WhenAudioTranscodeFails_ShouldReturnFailedResult()
    {
        // Arrange
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        var channel = CreateChannel(feedType: FeedType.Audio);
        var episode = CreateEpisode(channel);

        _ffmpegClientMock
            .Setup(f => f.TranscodeAudioAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AudioTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FfmpegException("Audio transcoding failed", 1, "Error output"));

        // Act
        var result = await service.TranscodeAsync("/tmp/input.mp4", channel, episode);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Audio transcoding failed");
    }

    [Fact]
    public async Task TranscodeAsync_WhenVideoTranscodeFails_ShouldReturnFailedResult()
    {
        // Arrange
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        var channel = CreateChannel(feedType: FeedType.Video);
        var episode = CreateEpisode(channel);

        _ffmpegClientMock
            .Setup(f => f.TranscodeVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<VideoTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FfmpegException("Video transcoding failed", 1, "Error output"));

        // Act
        var result = await service.TranscodeAsync("/tmp/input.mp4", channel, episode);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Video transcoding failed");
    }

    [Fact]
    public async Task TranscodeAsync_WhenBothFeedTypeAndAudioFails_ShouldReturnFailedResult()
    {
        // Arrange
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        var channel = CreateChannel(feedType: FeedType.Both);
        var episode = CreateEpisode(channel);

        _ffmpegClientMock
            .Setup(f => f.TranscodeAudioAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AudioTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FfmpegException("Audio transcoding failed", 1, "Error output"));

        // Act
        var result = await service.TranscodeAsync("/tmp/input.mp4", channel, episode);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Audio transcoding failed");
        
        // Video should not have been attempted
        _ffmpegClientMock.Verify(f => f.TranscodeVideoAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<VideoTranscodeSettings?>(),
            It.IsAny<Action<TranscodeProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranscodeAsync_WhenBothFeedTypeAndVideoFails_ShouldReturnFailedResult()
    {
        // Arrange
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        var channel = CreateChannel(feedType: FeedType.Both);
        var episode = CreateEpisode(channel);

        _ffmpegClientMock
            .Setup(f => f.TranscodeAudioAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AudioTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string output, AudioTranscodeSettings? _, Action<TranscodeProgress>? _, CancellationToken _) =>
                new TranscodeResult
                {
                    Success = true,
                    ExitCode = 0,
                    Duration = TimeSpan.FromSeconds(10),
                    OutputPath = output,
                    OutputFileSize = 1024 * 1024
                });

        _ffmpegClientMock
            .Setup(f => f.TranscodeVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<VideoTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FfmpegException("Video transcoding failed", 1, "Error output"));

        // Act
        var result = await service.TranscodeAsync("/tmp/input.mp4", channel, episode);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Video transcoding failed");
        
        // Audio should have succeeded but overall result is failure
        result.AudioPath.Should().NotBeNull();
        result.AudioFileSize.Should().Be(1024L * 1024);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task TranscodeAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        var channel = CreateChannel(feedType: FeedType.Audio);
        var episode = CreateEpisode(channel);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => service.TranscodeAsync("/tmp/input.mp4", channel, episode, progressCallback: null, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task TranscodeAsync_ShouldPassCancellationTokenToFfmpegClient()
    {
        // Arrange
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        var channel = CreateChannel(feedType: FeedType.Audio);
        var episode = CreateEpisode(channel);
        var cts = new CancellationTokenSource();

        _ffmpegClientMock
            .Setup(f => f.TranscodeAudioAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AudioTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string output, AudioTranscodeSettings? _, Action<TranscodeProgress>? _, CancellationToken _) =>
                new TranscodeResult
                {
                    Success = true,
                    ExitCode = 0,
                    Duration = TimeSpan.FromSeconds(10),
                    OutputPath = output,
                    OutputFileSize = 1024
                });

        // Act
        await service.TranscodeAsync("/tmp/input.mp4", channel, episode, progressCallback: null, cts.Token);

        // Assert
        _ffmpegClientMock.Verify(f => f.TranscodeAudioAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<AudioTranscodeSettings?>(),
            It.IsAny<Action<TranscodeProgress>?>(),
            cts.Token), Times.Once);
    }

    #endregion

    #region TranscodeServiceResult Tests

    [Fact]
    public void TranscodeServiceResult_ShouldStoreAllProperties()
    {
        // Arrange & Act
        var result = new TranscodeServiceResult
        {
            Success = true,
            AudioPath = "/downloads/channel123/audio/video.mp3",
            VideoPath = "/downloads/channel123/video/video.mp4",
            AudioFileSize = 1024 * 1024,
            VideoFileSize = 50 * 1024 * 1024,
            ErrorMessage = null
        };

        // Assert
        result.Success.Should().BeTrue();
        result.AudioPath.Should().Be("/downloads/channel123/audio/video.mp3");
        result.VideoPath.Should().Be("/downloads/channel123/video/video.mp4");
        result.AudioFileSize.Should().Be(1024L * 1024);
        result.VideoFileSize.Should().Be(50L * 1024 * 1024);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void TranscodeServiceResult_WithFailure_ShouldIndicateFailure()
    {
        // Arrange & Act
        var result = new TranscodeServiceResult
        {
            Success = false,
            ErrorMessage = "Transcoding failed due to invalid input"
        };

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Transcoding failed");
    }

    #endregion

    #region Progress Callback Tests

    [Fact]
    public async Task TranscodeAsync_WithProgressCallback_ShouldForwardProgress()
    {
        // Arrange
        var service = new TranscodeService(
            _loggerMock.Object,
            _ffmpegClientMock.Object,
            _defaultOptions,
            _defaultDownloadDir);

        var channel = CreateChannel(feedType: FeedType.Audio);
        var episode = CreateEpisode(channel);
        var progressReports = new List<TranscodeProgress>();

        _ffmpegClientMock
            .Setup(f => f.TranscodeAudioAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AudioTranscodeSettings?>(),
                It.IsAny<Action<TranscodeProgress>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, AudioTranscodeSettings?, Action<TranscodeProgress>?, CancellationToken>(
                (_, _, _, progress, _) =>
                {
                    // Simulate FFmpeg progress callback
                    progress?.Invoke(new TranscodeProgress { Frame = 100, Speed = 15.5 });
                })
            .ReturnsAsync(new TranscodeResult
            {
                Success = true,
                ExitCode = 0,
                Duration = TimeSpan.FromSeconds(10),
                OutputFileSize = 1024
            });

        // Act
        var result = await service.TranscodeAsync(
            "/tmp/input.mp4",
            channel,
            episode,
            progressCallback: p => progressReports.Add(p));

        // Assert
        result.Success.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static Channel CreateChannel(string? id = null, FeedType feedType = FeedType.Audio)
    {
        return new Channel
        {
            Id = id ?? "test-channel-id",
            Url = "https://youtube.com/@test",
            Title = "Test Channel",
            FeedType = feedType,
            EpisodeCountConfig = 50,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static Episode CreateEpisode(Channel channel, string? videoId = null)
    {
        return new Episode
        {
            Id = Guid.NewGuid().ToString("N"),
            VideoId = videoId ?? "video123",
            ChannelId = channel.Id,
            Title = "Test Episode",
            Description = "Test Description",
            DurationSeconds = 300,
            Status = EpisodeStatus.Processing,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    #endregion
}