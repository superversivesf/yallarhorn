namespace Yallarhorn.Services;

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Yallarhorn.Models;

/// <summary>
/// Interface for FFmpeg process execution operations.
/// </summary>
public interface IFfmpegClient
{
    /// <summary>
    /// Transcodes an audio/video file to an audio format (MP3, M4A, AAC, OGG).
    /// </summary>
    /// <param name="inputPath">The input file path.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="settings">Transcoding settings.</param>
    /// <param name="progressCallback">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the transcoding operation.</returns>
    /// <exception cref="FfmpegException">Thrown when transcoding fails.</exception>
    Task<TranscodeResult> TranscodeAudioAsync(
        string inputPath,
        string outputPath,
        AudioTranscodeSettings? settings = null,
        Action<TranscodeProgress>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transcodes a video file to H.264/MP4 format.
    /// </summary>
    /// <param name="inputPath">The input file path.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="settings">Transcoding settings.</param>
    /// <param name="progressCallback">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the transcoding operation.</returns>
    /// <exception cref="FfmpegException">Thrown when transcoding fails.</exception>
    Task<TranscodeResult> TranscodeVideoAsync(
        string inputPath,
        string outputPath,
        VideoTranscodeSettings? settings = null,
        Action<TranscodeProgress>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets media information from a file (duration, codec info, etc.).
    /// </summary>
    /// <param name="filePath">The file path to probe.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Media information.</returns>
    /// <exception cref="FfmpegException">Thrown when probing fails.</exception>
    Task<MediaInfo> GetMediaInfoAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Exception thrown when FFmpeg operations fail.
/// </summary>
public class FfmpegException : Exception
{
    /// <summary>
    /// Gets a value indicating whether the operation was successful (exit code 0).
    /// </summary>
    public bool Success => ExitCode == 0;

    /// <summary>
    /// Gets the exit code from the FFmpeg process.
    /// </summary>
    public int? ExitCode { get; }

    /// <summary>
    /// Gets the stderr output from FFmpeg.
    /// </summary>
    public string? ErrorOutput { get; }

    /// <summary>
    /// Creates a new FfmpegException with a message.
    /// </summary>
    public FfmpegException(string message) : base(message) { }

    /// <summary>
    /// Creates a new FfmpegException with a message and inner exception.
    /// </summary>
    public FfmpegException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    /// Creates a new FfmpegException with exit code and error output.
    /// </summary>
    public FfmpegException(string message, int exitCode, string? errorOutput = null)
        : base($"{message} (ExitCode: {exitCode})")
    {
        ExitCode = exitCode;
        ErrorOutput = errorOutput;
    }
}

/// <summary>
/// Client for executing FFmpeg commands and parsing output.
/// </summary>
public class FfmpegClient : IFfmpegClient
{
    private const string FfmpegExecutable = "ffmpeg";
    private const string FfprobeExecutable = "ffprobe";
    private readonly ILogger<FfmpegClient> _logger;
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(60);

    /// <summary>
    /// Initializes a new instance of the FfmpegClient.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public FfmpegClient(ILogger<FfmpegClient> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TranscodeResult> TranscodeAudioAsync(
        string inputPath,
        string outputPath,
        AudioTranscodeSettings? settings = null,
        Action<TranscodeProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Transcoding audio from {InputPath} to {OutputPath}", inputPath, outputPath);

        settings ??= new AudioTranscodeSettings();

        // Ensure output directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var arguments = BuildAudioTranscodeArguments(inputPath, outputPath, settings);
        var stopwatch = Stopwatch.StartNew();

        var result = await ExecuteFfmpegWithProgressAsync(arguments, progressCallback, cancellationToken);

        stopwatch.Stop();

        if (result.ExitCode != 0)
        {
            _logger.LogError("Audio transcoding failed for {InputPath}. Exit code: {ExitCode}, Error: {Error}",
                inputPath, result.ExitCode, result.Error);

            throw new FfmpegException(
                "Audio transcoding failed",
                result.ExitCode,
                result.Error);
        }

        var outputFileSize = GetFileSize(outputPath);

        _logger.LogInformation("Audio transcoding completed for {InputPath} in {Duration}s",
            inputPath, stopwatch.Elapsed.TotalSeconds);

        return new TranscodeResult
        {
            Success = true,
            ExitCode = 0,
            Duration = stopwatch.Elapsed,
            OutputPath = outputPath,
            OutputFileSize = outputFileSize
        };
    }

    /// <inheritdoc/>
    public async Task<TranscodeResult> TranscodeVideoAsync(
        string inputPath,
        string outputPath,
        VideoTranscodeSettings? settings = null,
        Action<TranscodeProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Transcoding video from {InputPath} to {OutputPath}", inputPath, outputPath);

        settings ??= new VideoTranscodeSettings();

        // Ensure output directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var arguments = BuildVideoTranscodeArguments(inputPath, outputPath, settings);
        var stopwatch = Stopwatch.StartNew();

        var result = await ExecuteFfmpegWithProgressAsync(arguments, progressCallback, cancellationToken);

        stopwatch.Stop();

        if (result.ExitCode != 0)
        {
            _logger.LogError("Video transcoding failed for {InputPath}. Exit code: {ExitCode}, Error: {Error}",
                inputPath, result.ExitCode, result.Error);

            throw new FfmpegException(
                "Video transcoding failed",
                result.ExitCode,
                result.Error);
        }

        var outputFileSize = GetFileSize(outputPath);

        _logger.LogInformation("Video transcoding completed for {InputPath} in {Duration}s",
            inputPath, stopwatch.Elapsed.TotalSeconds);

        return new TranscodeResult
        {
            Success = true,
            ExitCode = 0,
            Duration = stopwatch.Elapsed,
            OutputPath = outputPath,
            OutputFileSize = outputFileSize
        };
    }

    /// <inheritdoc/>
    public async Task<MediaInfo> GetMediaInfoAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting media info for {FilePath}", filePath);

        var arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"";

        var result = await ExecuteFfprobeAsync(arguments, cancellationToken);

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to get media info for {FilePath}. Exit code: {ExitCode}",
                filePath, result.ExitCode);

            throw new FfmpegException(
                "Failed to get media info",
                result.ExitCode,
                result.Error);
        }

        try
        {
            var mediaInfo = ParseMediaInfo(result.Output);
            _logger.LogInformation("Media info retrieved: Duration={Duration}, VideoCodec={VideoCodec}, AudioCodec={AudioCodec}",
                mediaInfo.Duration, mediaInfo.VideoCodec, mediaInfo.AudioCodec);

            return mediaInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse ffprobe output for {FilePath}", filePath);
            throw new FfmpegException("Failed to parse media info", ex);
        }
    }

    private static string BuildAudioTranscodeArguments(string inputPath, string outputPath, AudioTranscodeSettings settings)
    {
        var audioCodec = settings.Format.ToLowerInvariant() switch
        {
            "mp3" => "libmp3lame",
            "m4a" or "aac" => "aac",
            "ogg" => "libvorbis",
            _ => "libmp3lame"
        };

        return $"-i \"{inputPath}\" " +
               $"-vn " +
               $"-acodec {audioCodec} " +
               $"-b:a {settings.Bitrate} " +
               $"-ar {settings.SampleRate} " +
               $"-ac {settings.Channels} " +
               $"-y " +
               $"\"{outputPath}\"";
    }

    private static string BuildVideoTranscodeArguments(string inputPath, string outputPath, VideoTranscodeSettings settings)
    {
        return $"-i \"{inputPath}\" " +
               $"-c:v {settings.VideoCodec} " +
               $"-preset {settings.Preset} " +
               $"-crf {settings.Quality} " +
               $"-c:a aac " +
               $"-b:a {settings.AudioBitrate} " +
               $"-ar {settings.AudioSampleRate} " +
               $"-ac {settings.AudioChannels} " +
               $"-movflags +faststart " +
               $"-y " +
               $"\"{outputPath}\"";
    }

    private async Task<FfmpegResult> ExecuteFfmpegWithProgressAsync(
        string arguments,
        Action<TranscodeProgress>? progressCallback,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = FfmpegExecutable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        _logger.LogDebug("Executing: {Executable} {Arguments}", FfmpegExecutable, arguments);

        process.Start();

        // Read output stream asynchronously
        var outputTask = ReadStreamAsync(process.StandardOutput, outputBuilder);

        // Read error stream - with progress parsing
        Task errorTask;
        if (progressCallback != null)
        {
            errorTask = ReadStreamWithProgressAsync(process.StandardError, errorBuilder, progressCallback, cancellationToken);
        }
        else
        {
            errorTask = ReadStreamAsync(process.StandardError, errorBuilder);
        }

        // Wait for process to complete with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_defaultTimeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch { }

            throw new FfmpegException($"FFmpeg process timed out after {_defaultTimeout.TotalMinutes} minutes");
        }

        await outputTask;
        await errorTask;

        return new FfmpegResult
        {
            ExitCode = process.ExitCode,
            Output = outputBuilder.ToString(),
            Error = errorBuilder.ToString()
        };
    }

    private async Task<FfmpegResult> ExecuteFfprobeAsync(
        string arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = FfprobeExecutable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        _logger.LogDebug("Executing: {Executable} {Arguments}", FfprobeExecutable, arguments);

        process.Start();

        var outputTask = ReadStreamAsync(process.StandardOutput, outputBuilder);
        var errorTask = ReadStreamAsync(process.StandardError, errorBuilder);

        // Wait for process to complete with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch { }

            throw new FfmpegException("ffprobe process timed out");
        }

        await outputTask;
        await errorTask;

        return new FfmpegResult
        {
            ExitCode = process.ExitCode,
            Output = outputBuilder.ToString(),
            Error = errorBuilder.ToString()
        };
    }

    private static async Task ReadStreamAsync(StreamReader reader, StringBuilder builder)
    {
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            builder.AppendLine(line);
        }
    }

    private static async Task ReadStreamWithProgressAsync(
        StreamReader reader,
        StringBuilder builder,
        Action<TranscodeProgress> progressCallback,
        CancellationToken cancellationToken)
    {
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            builder.AppendLine(line);

            // Parse progress from the line and invoke callback
            var progress = ParseProgressLine(line);
            if (progress != null)
            {
                progressCallback(progress);
            }
        }
    }

    private static TranscodeProgress? ParseProgressLine(string line)
    {
        // FFmpeg progress lines are typically:
        // frame= 1234 fps= 60 q=-1.0 size= 12345kB time=00:01:02.34 bitrate=1234.5kbits/s speed=2.5x

        if (string.IsNullOrWhiteSpace(line))
            return null;

        var progress = new TranscodeProgress();

        // Extract frame number
        var frameMatch = Regex.Match(line, @"frame=\s*(\d+)");
        if (frameMatch.Success && long.TryParse(frameMatch.Groups[1].Value, out var frame))
        {
            progress = progress with { Frame = frame };
        }

        // Extract time
        var timeMatch = Regex.Match(line, @"time=(\d+):(\d+):(\d+\.?\d*)");
        if (timeMatch.Success)
        {
            var hours = int.Parse(timeMatch.Groups[1].Value);
            var minutes = int.Parse(timeMatch.Groups[2].Value);
            var seconds = double.Parse(timeMatch.Groups[3].Value);
            progress = progress with { Time = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds) };
        }

        // Extract bitrate
        var bitrateMatch = Regex.Match(line, @"bitrate=([\d.]+)([KMGT]?bits/s)");
        if (bitrateMatch.Success && double.TryParse(bitrateMatch.Groups[1].Value, out var bitrate))
        {
            var multiplier = bitrateMatch.Groups[2].Value.ToUpperInvariant() switch
            {
                "KBITS/S" => 1000,
                "MBITS/S" => 1000 * 1000,
                "GBITS/S" => 1000L * 1000 * 1000,
                _ => 1
            };
            progress = progress with { Bitrate = bitrate * multiplier };
        }

        // Extract speed
        var speedMatch = Regex.Match(line, @"speed=([\d.]+)x");
        if (speedMatch.Success && double.TryParse(speedMatch.Groups[1].Value, out var speed))
        {
            progress = progress with { Speed = speed };
        }

        // Return progress if we parsed any meaningful data
        if (progress.Frame.HasValue || progress.Time.HasValue || progress.Bitrate.HasValue || progress.Speed.HasValue)
        {
            return progress;
        }

        return null;
    }

    private static MediaInfo ParseMediaInfo(string jsonOutput)
    {
        using var document = System.Text.Json.JsonDocument.Parse(jsonOutput);
        var root = document.RootElement;

        // Find duration from format section
        TimeSpan duration = TimeSpan.Zero;
        if (root.TryGetProperty("format", out var format))
        {
            if (format.TryGetProperty("duration", out var durationElement) &&
                double.TryParse(durationElement.GetString(), out var durationSeconds))
            {
                duration = TimeSpan.FromSeconds(durationSeconds);
            }
        }

        // Parse streams
        string? videoCodec = null;
        string? audioCodec = null;
        int? width = null;
        int? height = null;
        int? audioSampleRate = null;
        int? audioChannels = null;
        double? frameRate = null;
        long? videoBitrate = null;
        long? audioBitrate = null;

        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var codecType = stream.TryGetProperty("codec_type", out var typeElement)
                    ? typeElement.GetString()
                    : null;

                if (codecType == "video" && videoCodec == null)
                {
                    videoCodec = stream.TryGetProperty("codec_name", out var codecElement)
                        ? codecElement.GetString()
                        : null;

                    if (stream.TryGetProperty("width", out var widthElement))
                        width = widthElement.GetInt32();

                    if (stream.TryGetProperty("height", out var heightElement))
                        height = heightElement.GetInt32();

                    if (stream.TryGetProperty("r_frame_rate", out var fpsElement))
                    {
                        var fpsStr = fpsElement.GetString();
                        if (!string.IsNullOrEmpty(fpsStr) && fpsStr.Contains('/'))
                        {
                            var parts = fpsStr.Split('/');
                            if (parts.Length == 2 &&
                                double.TryParse(parts[0], out var num) &&
                                double.TryParse(parts[1], out var den) &&
                                den > 0)
                            {
                                frameRate = num / den;
                            }
                        }
                    }

                    if (stream.TryGetProperty("bit_rate", out var vbElement) &&
                        long.TryParse(vbElement.GetString(), out var vb))
                    {
                        videoBitrate = vb;
                    }
                }
                else if (codecType == "audio" && audioCodec == null)
                {
                    audioCodec = stream.TryGetProperty("codec_name", out var codecElement)
                        ? codecElement.GetString()
                        : null;

                    if (stream.TryGetProperty("sample_rate", out var srElement) &&
                        int.TryParse(srElement.GetString(), out var sr))
                    {
                        audioSampleRate = sr;
                    }

                    if (stream.TryGetProperty("channels", out var chElement))
                        audioChannels = chElement.GetInt32();

                    if (stream.TryGetProperty("bit_rate", out var abElement) &&
                        long.TryParse(abElement.GetString(), out var ab))
                    {
                        audioBitrate = ab;
                    }
                }
            }
        }

        return new MediaInfo
        {
            Duration = duration,
            VideoCodec = videoCodec,
            AudioCodec = audioCodec,
            Width = width,
            Height = height,
            AudioSampleRate = audioSampleRate,
            AudioChannels = audioChannels,
            FrameRate = frameRate,
            VideoBitrate = videoBitrate,
            AudioBitrate = audioBitrate
        };
    }

    private static long? GetFileSize(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.Exists ? fileInfo.Length : null;
        }
        catch
        {
            return null;
        }
    }

    private class FfmpegResult
    {
        public int ExitCode { get; init; }
        public string Output { get; init; } = string.Empty;
        public string Error { get; init; } = string.Empty;
    }
}