namespace Yallarhorn.Services;

using Microsoft.Extensions.Logging;
using Yallarhorn.Configuration;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Models;

/// <summary>
/// Interface for transcoding service that orchestrates audio/video transcoding based on channel settings.
/// </summary>
public interface ITranscodeService
{
    /// <summary>
    /// Transcodes media based on the channel's feed type configuration.
    /// </summary>
    /// <param name="inputPath">Path to the input media file.</param>
    /// <param name="channel">The channel containing feed type configuration.</param>
    /// <param name="episode">The episode to update with file paths and sizes.</param>
    /// <param name="progressCallback">Optional callback for progress updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the transcoding operation.</returns>
    Task<TranscodeServiceResult> TranscodeAsync(
        string inputPath,
        Channel channel,
        Episode episode,
        Action<TranscodeProgress>? progressCallback = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a transcoding operation.
/// </summary>
public record TranscodeServiceResult
{
    /// <summary>
    /// Gets a value indicating whether the transcoding was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the path to the transcoded audio file, if audio was transcoded.
    /// </summary>
    public string? AudioPath { get; init; }

    /// <summary>
    /// Gets the path to the transcoded video file, if video was transcoded.
    /// </summary>
    public string? VideoPath { get; init; }

    /// <summary>
    /// Gets the audio file size in bytes, if audio was transcoded.
    /// </summary>
    public long? AudioFileSize { get; init; }

    /// <summary>
    /// Gets the video file size in bytes, if video was transcoded.
    /// </summary>
    public long? VideoFileSize { get; init; }

    /// <summary>
    /// Gets the error message if transcoding failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Service that orchestrates transcoding based on channel feed_type settings.
/// Handles audio-only, video-only, and dual output transcoding.
/// </summary>
public class TranscodeService : ITranscodeService
{
    private readonly ILogger<TranscodeService> _logger;
    private readonly IFfmpegClient _ffmpegClient;
    private readonly TranscodeOptions _options;
    private readonly string _downloadDirectory;

    /// <summary>
    /// Initializes a new instance of the TranscodeService.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="ffmpegClient">FFmpeg client for transcoding operations.</param>
    /// <param name="options">Transcode configuration options.</param>
    /// <param name="downloadDirectory">Base directory for downloaded files.</param>
    public TranscodeService(
        ILogger<TranscodeService> logger,
        IFfmpegClient ffmpegClient,
        TranscodeOptions options,
        string downloadDirectory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ffmpegClient = ffmpegClient ?? throw new ArgumentNullException(nameof(ffmpegClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _downloadDirectory = downloadDirectory ?? throw new ArgumentNullException(nameof(downloadDirectory));
    }

    /// <inheritdoc/>
    public async Task<TranscodeServiceResult> TranscodeAsync(
        string inputPath,
        Channel channel,
        Episode episode,
        Action<TranscodeProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputPath);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(episode);

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Starting transcoding for episode {EpisodeId}, channel {ChannelId}, feed type {FeedType}",
            episode.Id, channel.Id, channel.FeedType);

        string? audioPath = null;
        string? videoPath = null;
        long? audioFileSize = null;
        long? videoFileSize = null;

        try
        {
            // Transcode audio if needed
            if (channel.FeedType is FeedType.Audio or FeedType.Both)
            {
                var result = await TranscodeAudioAsync(inputPath, channel, episode, progressCallback, cancellationToken);

                if (!result.Success)
                {
                    return new TranscodeServiceResult
                    {
                        Success = false,
                        ErrorMessage = $"Audio transcoding failed: {result.ErrorOutput ?? "Unknown error"}"
                    };
                }

                audioPath = result.OutputPath;
                audioFileSize = result.OutputFileSize;

                // Update episode
                if (audioPath != null)
                {
                    episode.FilePathAudio = GetRelativePath(audioPath);
                }
                episode.FileSizeAudio = audioFileSize;

                _logger.LogInformation(
                    "Audio transcoding completed for episode {EpisodeId}. Size: {Size} bytes",
                    episode.Id, audioFileSize);
            }

            // Transcode video if needed
            if (channel.FeedType is FeedType.Video or FeedType.Both)
            {
                var result = await TranscodeVideoAsync(inputPath, channel, episode, progressCallback, cancellationToken);

                if (!result.Success)
                {
                    // If both feed type and audio succeeded, we still report failure
                    // but include the audio info
                    return new TranscodeServiceResult
                    {
                        Success = false,
                        AudioPath = audioPath,
                        AudioFileSize = audioFileSize,
                        ErrorMessage = $"Video transcoding failed: {result.ErrorOutput ?? "Unknown error"}"
                    };
                }

                videoPath = result.OutputPath;
                videoFileSize = result.OutputFileSize;

                // Update episode
                if (videoPath != null)
                {
                    episode.FilePathVideo = GetRelativePath(videoPath);
                }
                episode.FileSizeVideo = videoFileSize;

                _logger.LogInformation(
                    "Video transcoding completed for episode {EpisodeId}. Size: {Size} bytes",
                    episode.Id, videoFileSize);
            }

            _logger.LogInformation(
                "Transcoding completed successfully for episode {EpisodeId}. Audio: {AudioPath}, Video: {VideoPath}",
                episode.Id, audioPath ?? "none", videoPath ?? "none");

            return new TranscodeServiceResult
            {
                Success = true,
                AudioPath = audioPath,
                VideoPath = videoPath,
                AudioFileSize = audioFileSize,
                VideoFileSize = videoFileSize
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Transcoding cancelled for episode {EpisodeId}", episode.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcoding failed for episode {EpisodeId}", episode.Id);
            return new TranscodeServiceResult
            {
                Success = false,
                AudioPath = audioPath,
                VideoPath = videoPath,
                AudioFileSize = audioFileSize,
                VideoFileSize = videoFileSize,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<TranscodeResult> TranscodeAudioAsync(
        string inputPath,
        Channel channel,
        Episode episode,
        Action<TranscodeProgress>? progressCallback,
        CancellationToken cancellationToken)
    {
        var outputPath = GenerateAudioPath(channel.Id, episode.VideoId);
        var settings = CreateAudioSettings();

        _logger.LogDebug(
            "Transcoding audio from {InputPath} to {OutputPath} with format {Format}",
            inputPath, outputPath, settings.Format);

        try
        {
            return await _ffmpegClient.TranscodeAudioAsync(
                inputPath,
                outputPath,
                settings,
                progressCallback,
                cancellationToken);
        }
        catch (FfmpegException ex)
        {
            _logger.LogError(ex, "Audio transcoding failed for episode {EpisodeId}", episode.Id);
            return new TranscodeResult
            {
                Success = false,
                ExitCode = ex.ExitCode ?? -1,
                ErrorOutput = ex.ErrorOutput ?? ex.Message
            };
        }
    }

    private async Task<TranscodeResult> TranscodeVideoAsync(
        string inputPath,
        Channel channel,
        Episode episode,
        Action<TranscodeProgress>? progressCallback,
        CancellationToken cancellationToken)
    {
        var outputPath = GenerateVideoPath(channel.Id, episode.VideoId);
        var settings = CreateVideoSettings();

        _logger.LogDebug(
            "Transcoding video from {InputPath} to {OutputPath} with codec {Codec}",
            inputPath, outputPath, settings.VideoCodec);

        try
        {
            return await _ffmpegClient.TranscodeVideoAsync(
                inputPath,
                outputPath,
                settings,
                progressCallback,
                cancellationToken);
        }
        catch (FfmpegException ex)
        {
            _logger.LogError(ex, "Video transcoding failed for episode {EpisodeId}", episode.Id);
            return new TranscodeResult
            {
                Success = false,
                ExitCode = ex.ExitCode ?? -1,
                ErrorOutput = ex.ErrorOutput ?? ex.Message
            };
        }
    }

    /// <summary>
    /// Generates the output path for an audio file.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="videoId">The video ID.</param>
    /// <returns>The full path to the audio output file.</returns>
    private string GenerateAudioPath(string channelId, string videoId)
    {
        var format = _options.AudioFormat.ToLowerInvariant();
        return Path.Combine(
            _downloadDirectory,
            channelId,
            "audio",
            $"{videoId}.{format}");
    }

    /// <summary>
    /// Generates the output path for a video file.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="videoId">The video ID.</param>
    /// <returns>The full path to the video output file.</returns>
    private string GenerateVideoPath(string channelId, string videoId)
    {
        var format = _options.VideoFormat.ToLowerInvariant();
        return Path.Combine(
            _downloadDirectory,
            channelId,
            "video",
            $"{videoId}.{format}");
    }

    /// <summary>
    /// Converts an absolute path to a relative path for storage.
    /// </summary>
    /// <param name="absolutePath">The absolute file path.</param>
    /// <returns>A relative path from the download directory.</returns>
    private string GetRelativePath(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
            return absolutePath;

        var fullPath = Path.GetFullPath(absolutePath);
        var downloadDir = Path.GetFullPath(_downloadDirectory);

        if (fullPath.StartsWith(downloadDir, StringComparison.OrdinalIgnoreCase))
        {
            var relative = fullPath.Substring(downloadDir.Length);
            return relative.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return absolutePath;
    }

    /// <summary>
    /// Creates audio transcode settings from configuration.
    /// </summary>
    /// <returns>Audio transcode settings.</returns>
    private AudioTranscodeSettings CreateAudioSettings()
    {
        return new AudioTranscodeSettings
        {
            Format = _options.AudioFormat.ToLowerInvariant(),
            Bitrate = _options.AudioBitrate,
            SampleRate = _options.AudioSampleRate,
            Channels = 2 // Stereo
        };
    }

    /// <summary>
    /// Creates video transcode settings from configuration.
    /// </summary>
    /// <returns>Video transcode settings.</returns>
    private VideoTranscodeSettings CreateVideoSettings()
    {
        // Map codec configuration to FFmpeg codec names
        var videoCodec = _options.VideoCodec.ToLowerInvariant() switch
        {
            "h264" => "libx264",
            "h265" or "hevc" => "libx265",
            "vp9" => "libvpx-vp9",
            "av1" => "libaom-av1",
            _ => "libx264"
        };

        return new VideoTranscodeSettings
        {
            Format = _options.VideoFormat.ToLowerInvariant(),
            VideoCodec = videoCodec,
            Preset = "medium",
            Quality = _options.VideoQuality,
            AudioBitrate = _options.AudioBitrate,
            AudioSampleRate = _options.AudioSampleRate,
            AudioChannels = 2 // Stereo
        };
    }
}