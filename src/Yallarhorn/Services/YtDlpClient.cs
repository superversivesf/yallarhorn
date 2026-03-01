namespace Yallarhorn.Services;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Yallarhorn.Models;

/// <summary>
/// Client for executing yt-dlp commands and parsing output.
/// </summary>
public class YtDlpClient : IYtDlpClient
{
    private const string YtDlpExecutable = "yt-dlp";
    private readonly ILogger<YtDlpClient> _logger;
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Initializes a new instance of the YtDlpClient.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public YtDlpClient(ILogger<YtDlpClient> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<YtDlpMetadata> GetVideoMetadataAsync(string url, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching metadata for URL: {Url}", url);

        var arguments = "--print-json --no-download --no-warnings";
        var result = await ExecuteYtDlpAsync(arguments, url, cancellationToken);

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to fetch metadata for {Url}. Exit code: {ExitCode}, Error: {Error}",
                url, result.ExitCode, result.Error);
            throw new YtDlpException(
                $"Failed to fetch video metadata",
                result.ExitCode,
                result.Error);
        }

        try
        {
            var metadata = JsonSerializer.Deserialize<YtDlpMetadata>(result.Output, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (metadata == null)
            {
                throw new YtDlpException("Failed to parse video metadata: null result");
            }

            _logger.LogInformation("Successfully fetched metadata for video {VideoId}", metadata.Id);
            return metadata;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse yt-dlp JSON output for {Url}", url);
            throw new YtDlpException("Failed to parse video metadata", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<YtDlpMetadata>> GetChannelVideosAsync(string channelUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching channel videos from: {ChannelUrl}", channelUrl);

        // Note: --print-json outputs to stderr, not stdout
        var arguments = "--flat-playlist --print-json --no-warnings";
        var result = await ExecuteYtDlpAsync(arguments, channelUrl, cancellationToken);

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to fetch channel videos from {ChannelUrl}. Exit code: {ExitCode}, Error: {Error}",
                channelUrl, result.ExitCode, result.Error);
            throw new YtDlpException(
                "Failed to fetch channel videos",
                result.ExitCode,
                result.Error);
        }

        // yt-dlp outputs JSON to stdout (as of 2024.04.09)
        // But some versions may output to stderr, so check both
        var output = result.Output;
        if (string.IsNullOrEmpty(output))
        {
            output = result.Error;
            _logger.LogDebug("JSON output was on stderr, length: {Length}", output?.Length ?? 0);
        }
        else
        {
            _logger.LogDebug("JSON output was on stdout, length: {Length}", output.Length);
        }

        var videos = new List<YtDlpMetadata>();
        
        if (string.IsNullOrEmpty(output))
        {
            _logger.LogWarning("No output from yt-dlp for channel {ChannelUrl}. ExitCode: {ExitCode}", channelUrl, result.ExitCode);
            return videos.AsReadOnly();
        }

        var jsonLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        _logger.LogDebug("Parsing {LineCount} lines of yt-dlp output", jsonLines.Length);

        foreach (var line in jsonLines)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.TrimStart().StartsWith("{"))
            {
                continue;
            }

            try
            {
                var video = JsonSerializer.Deserialize<YtDlpMetadata>(line, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (video != null)
                {
                    videos.Add(video);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse video metadata line: {Line}", line);
            }
        }

        _logger.LogInformation("Found {Count} videos in channel", videos.Count);
        return videos.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<string> DownloadVideoAsync(
        string url,
        string outputPath,
        Action<DownloadProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Downloading video from {Url} to {OutputPath}", url, outputPath);

        // Ensure output directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var arguments = BuildDownloadArguments(outputPath);
        var result = await ExecuteYtDlpWithProgressAsync(arguments, url, progressCallback, cancellationToken);

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to download video from {Url}. Exit code: {ExitCode}, Error: {Error}",
                url, result.ExitCode, result.Error);
            throw new YtDlpException(
                "Failed to download video",
                result.ExitCode,
                result.Error);
        }

        if (!File.Exists(outputPath))
        {
            throw new YtDlpException($"Download completed but file not found at {outputPath}");
        }

        _logger.LogInformation("Successfully downloaded video to {OutputPath}", outputPath);
        return outputPath;
    }

    private static string BuildDownloadArguments(string outputPath)
    {
        // Use template for output, get best mp4 format or fallback
        return $"-f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\" " +
               $"--merge-output-format mp4 " +
               $"--no-playlist " +
               $"--no-overwrites " +
               $"--continue " +
               $"--no-warnings " +
               $"-o \"{outputPath}\"";
    }

    private async Task<YtDlpResult> ExecuteYtDlpAsync(
        string arguments,
        string url,
        CancellationToken cancellationToken)
    {
        return await ExecuteYtDlpWithProgressAsync(arguments, url, null, cancellationToken);
    }

    private async Task<YtDlpResult> ExecuteYtDlpWithProgressAsync(
        string arguments,
        string url,
        Action<DownloadProgress>? progressCallback,
        CancellationToken cancellationToken)
    {
        var fullArguments = $"{arguments} \"{url}\"";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = YtDlpExecutable,
                Arguments = fullArguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.StartInfo.RedirectStandardInput = false;

        _logger.LogDebug("Executing: {Executable} {Arguments}", YtDlpExecutable, fullArguments);

        process.Start();

        // Read output stream asynchronously
        var outputTask = ReadStreamAsync(process.StandardOutput, outputBuilder);

        // Read error stream - either with progress parsing or just capturing
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

            throw new YtDlpException($"yt-dlp process timed out after {_defaultTimeout.TotalMinutes} minutes");
        }

        await outputTask;
        await errorTask;

        return new YtDlpResult
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
        Action<DownloadProgress> progressCallback,
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

    private static DownloadProgress? ParseProgressLine(string line)
    {
        // yt-dlp progress lines are typically in format:
        // [download]  50.3% of 123.45MiB at 1.23MiB/s ETA 00:30
        if (!line.Contains("[download]"))
            return null;

        var progress = new DownloadProgress { Status = "downloading" };

        // Extract percentage
        var percentMatch = System.Text.RegularExpressions.Regex.Match(line, @"(\d+\.?\d*)%");
        if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, out var percent))
        {
            progress = progress with { Progress = percent };
        }

        // Extract download speed
        var speedMatch = System.Text.RegularExpressions.Regex.Match(line, @"at\s+([\d.]+[KMGT]?i?B/s)");
        if (speedMatch.Success)
        {
            var speedStr = speedMatch.Groups[1].Value;
            progress = progress with { Speed = ParseSpeed(speedStr) };
        }

        // Extract ETA
        var etaMatch = System.Text.RegularExpressions.Regex.Match(line, @"ETA\s+(\d+:\d+(?::\d+)?)");
        if (etaMatch.Success)
        {
            progress = progress with { Eta = ParseEta(etaMatch.Groups[1].Value) };
        }

        // Check for completion
        if (line.Contains("has already been downloaded") || line.Contains("Merging"))
        {
            progress = progress with { Status = "finished" };
        }

        return progress;
    }

    private static double ParseSpeed(string speedStr)
    {
        var match = System.Text.RegularExpressions.Regex.Match(speedStr, @"([\d.]+)([KMGT]?)(i?B/s)");
        if (!match.Success) return 0;

        var value = double.Parse(match.Groups[1].Value);
        var multiplier = match.Groups[2].Value.ToUpperInvariant() switch
        {
            "K" => 1024,
            "M" => 1024 * 1024,
            "G" => 1024 * 1024 * 1024,
            "T" => 1024L * 1024 * 1024 * 1024,
            _ => 1
        };
        return value * multiplier;
    }

    private static TimeSpan ParseEta(string etaStr)
    {
        var parts = etaStr.Split(':');
        return parts.Length switch
        {
            2 => TimeSpan.FromMinutes(int.Parse(parts[0])) + TimeSpan.FromSeconds(int.Parse(parts[1])),
            3 => TimeSpan.FromHours(int.Parse(parts[0])) + TimeSpan.FromMinutes(int.Parse(parts[1])) + TimeSpan.FromSeconds(int.Parse(parts[2])),
            _ => TimeSpan.Zero
        };
    }

    private class YtDlpResult
    {
        public int ExitCode { get; init; }
        public string Output { get; init; } = string.Empty;
        public string Error { get; init; } = string.Empty;
    }
}