namespace Yallarhorn.Tests.Unit.Services;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Yallarhorn.Models;
using Yallarhorn.Services;

public class FfmpegClientTests
{
    private readonly Mock<ILogger<FfmpegClient>> _loggerMock;
    private readonly FfmpegClient _client;

    public FfmpegClientTests()
    {
        _loggerMock = new Mock<ILogger<FfmpegClient>>();
        _client = new FfmpegClient(_loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidLogger_ShouldCreateInstance()
    {
        // Arrange & Act
        var client = new FfmpegClient(_loggerMock.Object);

        // Assert
        client.Should().NotBeNull();
    }

    #endregion

    #region TranscodeAudioAsync Tests

    [Fact]
    public async Task TranscodeAudioAsync_WithNonExistentInput_ShouldThrowFfmpegException()
    {
        // Arrange
        var inputPath = "/nonexistent/input.mp4";
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.mp3");
        var settings = new AudioTranscodeSettings();

        // Act
        var act = () => _client.TranscodeAudioAsync(inputPath, outputPath, settings);

        // Assert
        await act.Should().ThrowAsync<FfmpegException>()
            .Where(e => !e.Success);

        // Cleanup
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task TranscodeAudioAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var inputPath = "/nonexistent/input.mp4";
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.mp3");
        var settings = new AudioTranscodeSettings();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _client.TranscodeAudioAsync(inputPath, outputPath, settings, cancellationToken: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        // Cleanup
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task TranscodeAudioAsync_WithProgressCallback_ShouldAcceptCallback()
    {
        // Arrange
        var inputPath = "/nonexistent/input.mp4";
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.mp3");
        var settings = new AudioTranscodeSettings();
        var progressReports = new List<TranscodeProgress>();

        // Act & Assert - Will fail due to nonexistent file but callback parameter is tested
        var act = () => _client.TranscodeAudioAsync(
            inputPath, 
            outputPath, 
            settings, 
            progressCallback: p => progressReports.Add(p));

        await act.Should().ThrowAsync<FfmpegException>();

        // Cleanup
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }

    #endregion

    #region TranscodeVideoAsync Tests

    [Fact]
    public async Task TranscodeVideoAsync_WithNonExistentInput_ShouldThrowFfmpegException()
    {
        // Arrange
        var inputPath = "/nonexistent/input.mkv";
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.mp4");
        var settings = new VideoTranscodeSettings();

        // Act
        var act = () => _client.TranscodeVideoAsync(inputPath, outputPath, settings);

        // Assert
        await act.Should().ThrowAsync<FfmpegException>()
            .Where(e => !e.Success);

        // Cleanup
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task TranscodeVideoAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var inputPath = "/nonexistent/input.mkv";
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.mp4");
        var settings = new VideoTranscodeSettings();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _client.TranscodeVideoAsync(inputPath, outputPath, settings, cancellationToken: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        // Cleanup
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }

    #endregion

    #region GetMediaInfoAsync Tests

    [Fact]
    public async Task GetMediaInfoAsync_WithNonExistentFile_ShouldThrowFfmpegException()
    {
        // Arrange
        var filePath = "/nonexistent/video.mp4";

        // Act
        var act = () => _client.GetMediaInfoAsync(filePath);

        // Assert
        await act.Should().ThrowAsync<FfmpegException>();
    }

    [Fact]
    public async Task GetMediaInfoAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var filePath = "/nonexistent/video.mp4";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => _client.GetMediaInfoAsync(filePath, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region FfmpegException Tests

    [Fact]
    public void FfmpegException_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var message = "FFmpeg error";

        // Act
        var exception = new FfmpegException(message);

        // Assert
        exception.Message.Should().Be(message);
        exception.ExitCode.Should().BeNull();
        exception.ErrorOutput.Should().BeNull();
    }

    [Fact]
    public void FfmpegException_WithExitCodeAndError_ShouldSetProperties()
    {
        // Arrange
        var message = "Transcoding failed";
        var exitCode = 1;
        var errorOutput = "Error: Invalid data found";

        // Act
        var exception = new FfmpegException(message, exitCode, errorOutput);

        // Assert
        exception.Message.Should().Contain(message);
        exception.ExitCode.Should().Be(exitCode);
        exception.ErrorOutput.Should().Be(errorOutput);
        exception.Success.Should().BeFalse();
    }

    [Fact]
    public void FfmpegException_WithExitCodeZero_ShouldBeSuccess()
    {
        // Arrange
        var exitCode = 0;

        // Act
        var exception = new FfmpegException("Completed", exitCode);

        // Assert
        exception.ExitCode.Should().Be(0);
        exception.Success.Should().BeTrue();
    }

    [Fact]
    public void FfmpegException_WithInnerException_ShouldPreserveInner()
    {
        // Arrange
        var message = "FFmpeg failed";
        var innerException = new InvalidOperationException("Process failed");

        // Act
        var exception = new FfmpegException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().Be(innerException);
    }

    #endregion

    #region TranscodeResult Tests

    [Fact]
    public void TranscodeResult_ShouldAllowRecordWithExpression()
    {
        // Arrange
        var result = new TranscodeResult
        {
            Success = true,
            ExitCode = 0,
            Duration = TimeSpan.FromSeconds(10),
            ErrorOutput = null,
            OutputPath = "/output/video.mp4",
            OutputFileSize = 1024 * 1024
        };

        // Act
        var updated = result with { Duration = TimeSpan.FromSeconds(20) };

        // Assert
        updated.Success.Should().BeTrue();
        updated.Duration.Should().Be(TimeSpan.FromSeconds(20));
        result.Duration.Should().Be(TimeSpan.FromSeconds(10)); // Original unchanged
    }

    [Fact]
    public void TranscodeResult_WithSuccessFalse_ShouldIndicateFailure()
    {
        // Arrange & Act
        var result = new TranscodeResult
        {
            Success = false,
            ExitCode = 1,
            Duration = TimeSpan.Zero,
            ErrorOutput = "Error during transcoding"
        };

        // Assert
        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(1);
        result.ErrorOutput.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region MediaInfo Tests

    [Fact]
    public void MediaInfo_ShouldStoreAllProperties()
    {
        // Arrange & Act
        var info = new MediaInfo
        {
            Duration = TimeSpan.FromMinutes(5),
            VideoCodec = "h264",
            AudioCodec = "aac",
            Width = 1920,
            Height = 1080,
            AudioSampleRate = 44100,
            AudioChannels = 2,
            VideoBitrate = 5_000_000,
            AudioBitrate = 192_000,
            FrameRate = 30.0,
            OverallBitrate = 5_200_000
        };

        // Assert
        info.Duration.Should().Be(TimeSpan.FromMinutes(5));
        info.VideoCodec.Should().Be("h264");
        info.AudioCodec.Should().Be("aac");
        info.Width.Should().Be(1920);
        info.Height.Should().Be(1080);
    }

    [Fact]
    public void MediaInfo_WithMinimalData_ShouldBeValid()
    {
        // Arrange & Act
        var info = new MediaInfo
        {
            Duration = TimeSpan.FromSeconds(100)
        };

        // Assert
        info.Duration.Should().Be(TimeSpan.FromSeconds(100));
        info.VideoCodec.Should().BeNull();
        info.AudioCodec.Should().BeNull();
    }

    #endregion

    #region AudioTranscodeSettings Tests

    [Fact]
    public void AudioTranscodeSettings_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var settings = new AudioTranscodeSettings();

        // Assert
        settings.Format.Should().Be("mp3");
        settings.Bitrate.Should().Be("192k");
        settings.SampleRate.Should().Be(44100);
        settings.Channels.Should().Be(2);
    }

    [Theory]
    [InlineData("mp3")]
    [InlineData("m4a")]
    [InlineData("aac")]
    [InlineData("ogg")]
    public void AudioTranscodeSettings_WithValidFormat_ShouldAccept(string format)
    {
        // Arrange & Act
        var settings = new AudioTranscodeSettings { Format = format };

        // Assert
        settings.Format.Should().Be(format);
    }

    #endregion

    #region VideoTranscodeSettings Tests

    [Fact]
    public void VideoTranscodeSettings_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var settings = new VideoTranscodeSettings();

        // Assert
        settings.Format.Should().Be("mp4");
        settings.VideoCodec.Should().Be("libx264");
        settings.Preset.Should().Be("medium");
        settings.Quality.Should().Be(23);
        settings.AudioBitrate.Should().Be("192k");
        settings.AudioSampleRate.Should().Be(44100);
        settings.AudioChannels.Should().Be(2);
    }

    [Theory]
    [InlineData("ultrafast")]
    [InlineData("fast")]
    [InlineData("medium")]
    [InlineData("slow")]
    public void VideoTranscodeSettings_WithValidPreset_ShouldAccept(string preset)
    {
        // Arrange & Act
        var settings = new VideoTranscodeSettings { Preset = preset };

        // Assert
        settings.Preset.Should().Be(preset);
    }

    [Theory]
    [InlineData(18)]
    [InlineData(23)]
    [InlineData(28)]
    [InlineData(35)]
    public void VideoTranscodeSettings_WithValidQuality_ShouldAccept(int quality)
    {
        // Arrange & Act
        var settings = new VideoTranscodeSettings { Quality = quality };

        // Assert
        settings.Quality.Should().Be(quality);
    }

    #endregion

    #region TranscodeProgress Tests

    [Fact]
    public void TranscodeProgress_ShouldStoreProgressInformation()
    {
        // Arrange & Act
        var progress = new TranscodeProgress
        {
            Frame = 1500,
            Time = TimeSpan.FromSeconds(60),
            Bitrate = 1500.5,
            Speed = 15.2,
            Progress = 50.0
        };

        // Assert
        progress.Frame.Should().Be(1500);
        progress.Time.Should().Be(TimeSpan.FromSeconds(60));
        progress.Bitrate.Should().Be(1500.5);
        progress.Speed.Should().Be(15.2);
        progress.Progress.Should().Be(50.0);
    }

    [Fact]
    public void TranscodeProgress_ShouldAllowWithExpression()
    {
        // Arrange
        var progress = new TranscodeProgress
        {
            Frame = 100,
            Progress = 10
        };

        // Act
        var updated = progress with { Frame = 200, Progress = 20 };

        // Assert
        updated.Frame.Should().Be(200);
        updated.Progress.Should().Be(20);
        progress.Frame.Should().Be(100); // Original unchanged
    }

    #endregion

    #region IFfmpegClient Interface Tests

    [Fact]
    public void FfmpegClient_ShouldImplementIFfmpegClient()
    {
        // Arrange
        var client = new FfmpegClient(_loggerMock.Object);

        // Assert
        client.Should().BeAssignableTo<IFfmpegClient>();
    }

    #endregion
}