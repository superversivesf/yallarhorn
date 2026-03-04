namespace Yallarhorn.Services;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Yallarhorn.Configuration;
using Yallarhorn.Models;

/// <summary>
/// Client for executing yt-dlp commands with rate limiting and retry support.
/// </summary>
public class YtDlpClient : IYtDlpClient
{
    private const string YtDlpExecutable = "yt-dlp";
    private readonly ILogger<YtDlpClient> _logger;
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(30);
    private readonly YtdlpOptions _options;
    private readonly Random _random = new();
    private readonly SemaphoreSlim _rateLimitLock = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of the YtDlpClient.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="options">yt-dlp configuration options.</param>
    public YtDlpClient(ILogger<YtDlpClient> logger, YtdlpOptions options)
    {
        _logger = logger;
        _options = options;

        LogConfiguration();
    }

    private void LogConfiguration()
    {
        if (!string.IsNullOrEmpty(_options.CookiesPath))
        {
            if (File.Exists(_options.CookiesPath))
            {
                _logger.LogInformation("yt-dlp configured with cookies from {Path}", _options.CookiesPath);
            }
            else
            {
                _logger.LogWarning("Cookies file not found at {Path}", _options.CookiesPath);
            }
        }

        if (!string.IsNullOrEmpty(_options.ProxyUrl))
        {
            _logger.LogInformation("yt-dlp configured with proxy: {ProxyUrl}", _options.ProxyUrl);
        }

        if (_options.MinRequestDelaySeconds > 0)
        {
            _logger.LogInformation("yt-dlp rate limiting enabled: {Min}-{Max}s delay, backoff: {Backoff}, retries: {Retries}",
                _options.MinRequestDelaySeconds,
                _options.MaxRequestDelaySeconds,
                _options.EnableExponentialBackoff ? "enabled" : "disabled",
                _options.MaxRetries);
        }
    }

    /// <inheritdoc/>
    public async Task<YtDlpMetadata> GetVideoMetadataAsync(string url, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("Fetching metadata for URL: {Url}", url);

            await EnforceRateLimitAsync(cancellationToken);

            var arguments = "--print-json --no-download --no-warnings";
            var result = await ExecuteYtDlpAsync(arguments, url, cancellationToken);

            if (result.ExitCode != 0)
            {
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

                _logger.LogInformation("Fetched metadata for video {VideoId}", metadata.Id);
                return metadata;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse yt-dlp JSON output for {Url}", url);
                throw new YtDlpException("Failed to parse video metadata", ex);
            }
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<YtDlpMetadata>> GetChannelVideosAsync(string channelUrl, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("Fetching channel videos from: {ChannelUrl}", channelUrl);

            await EnforceRateLimitAsync(cancellationToken);

            var arguments = "--print-json --no-warnings --no-playlist-reverse --playlist-end 100";
            var result = await ExecuteYtDlpAsync(arguments, channelUrl, cancellationToken);

            if (result.ExitCode != 0)
            {
                throw new YtDlpException(
                    "Failed to fetch channel videos",
                    result.ExitCode,
                    result.Error);
            }

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
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<string> DownloadVideoAsync(
        string url,
        string outputPath,
        Action<DownloadProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("Downloading video from {Url} to {OutputPath}", url, outputPath);

            await EnforceRateLimitAsync(cancellationToken);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var arguments = BuildDownloadArguments(outputPath);
            var result = await ExecuteYtDlpWithProgressAsync(arguments, url, progressCallback, cancellationToken);

            if (result.ExitCode != 0)
            {
                throw new YtDlpException(
                    $"Failed to download video (ExitCode: {result.ExitCode}): {result.Error}",
                    result.ExitCode,
                    result.Error);
            }

            if (!File.Exists(outputPath))
            {
                throw new YtDlpException($"Download completed but file not found at {outputPath}");
            }

            _logger.LogInformation("Downloaded video to {OutputPath}", outputPath);
            return outputPath;
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<string?> DownloadThumbnailAsync(
        string videoId,
        string outputDir,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Downloading thumbnail for video {VideoId}", videoId);

        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var arguments = $"--write-thumbnail --no-download --no-warnings -o \"{outputDir}/{videoId}.%(ext)s\" \"https://www.youtube.com/watch?v={videoId}\"";

        try
        {
            await EnforceRateLimitAsync(cancellationToken);
            var result = await ExecuteYtDlpAsync(arguments, $"https://www.youtube.com/watch?v={videoId}", cancellationToken);

            if (result.ExitCode != 0)
            {
                _logger.LogWarning("Failed to download thumbnail for video {VideoId}: {Error}", videoId, result.Error);
                return null;
            }

            var files = Directory.GetFiles(outputDir, $"{videoId}.*")
                .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (files.Count == 0)
            {
                _logger.LogWarning("No thumbnail file found for video {VideoId}", videoId);
                return null;
            }

            var thumbnail = files.FirstOrDefault(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                          ?? files.FirstOrDefault(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                          ?? files.First();

            _logger.LogDebug("Downloaded thumbnail for video {VideoId}: {Path}", videoId, thumbnail);
            return thumbnail;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading thumbnail for video {VideoId}", videoId);
            return null;
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var attempt = 0;
        var currentDelay = _options.MinRequestDelaySeconds;

        while (true)
        {
            attempt++;

            try
            {
                return await operation();
            }
            catch (YtDlpException ex) when (IsRetryableError(ex.ErrorOutput) && attempt <= _options.MaxRetries)
            {
                var delay = currentDelay;

                if (_options.EnableExponentialBackoff)
                {
                    currentDelay = Math.Min(currentDelay * 2, _options.MaxBackoffSeconds);
                }

                _logger.LogWarning(
                    "yt-dlp encountered rate limit (attempt {Attempt}/{Max}). Waiting {Delay}s before retry. Error: {Error}",
                    attempt,
                    _options.MaxRetries,
                    delay,
                    ex.ErrorOutput);

                await Task.Delay(delay * 1000, cancellationToken);
            }
        }
    }

    private static bool IsRetryableError(string? error)
    {
        if (string.IsNullOrEmpty(error))
            return false;

        var lower = error.ToLowerInvariant();
        return lower.Contains("429") ||
               lower.Contains("rate limit") ||
               lower.Contains("too many requests") ||
               lower.Contains("sign in") ||
               lower.Contains("bot") ||
               lower.Contains("confirm");
    }

    private async Task EnforceRateLimitAsync(CancellationToken cancellationToken)
    {
        if (_options.MinRequestDelaySeconds <= 0)
        {
            return;
        }

        await _rateLimitLock.WaitAsync(cancellationToken);

        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            var randomJitter = _random.Next(
                _options.MinRequestDelaySeconds * 1000,
                _options.MaxRequestDelaySeconds * 1000 + 1);
            var minDelay = TimeSpan.FromMilliseconds(randomJitter);
            var actualDelay = minDelay - timeSinceLastRequest;

            if (actualDelay > TimeSpan.Zero)
            {
                _logger.LogDebug("Rate limiting: waiting {Delay:F1}s before yt-dlp call", actualDelay.TotalSeconds);
                await Task.Delay(actualDelay, cancellationToken);
            }

            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _rateLimitLock.Release();
        }
    }

    private string BuildGlobalArguments()
    {
        var args = new List<string>();

        if (!string.IsNullOrEmpty(_options.CookiesPath) && File.Exists(_options.CookiesPath))
        {
            args.Add($"--cookies \"{_options.CookiesPath}\"");
        }

        if (!string.IsNullOrEmpty(_options.ProxyUrl))
        {
            args.Add($"--proxy \"{_options.ProxyUrl}\"");
        }

        return string.Join(" ", args);
    }

    private static string BuildDownloadArguments(string outputPath)
    {
        return $"-f \"bestvideo[height<=480][ext=mp4]+bestaudio[ext=m4a]/bestvideo[height<=480][ext=mp4]+bestaudio/best[height<=480][ext=mp4]/best[height<=480]\" " +
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
        var globalArgs = BuildGlobalArguments();
        var fullArguments = string.IsNullOrEmpty(globalArgs)
            ? $"{arguments} \"{url}\""
            : $"{globalArgs} {arguments} \"{url}\"";

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

        var outputTask = ReadStreamAsync(process.StandardOutput, outputBuilder);

        Task errorTask;
        if (progressCallback != null)
        {
            errorTask = ReadStreamWithProgressAsync(process.StandardError, errorBuilder, progressCallback, cancellationToken);
        }
        else
        {
            errorTask = ReadStreamAsync(process.StandardError, errorBuilder);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_defaultTimeout);

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

            var progress = ParseProgressLine(line);
            if (progress != null)
            {
                progressCallback(progress);
            }
        }
    }

    private static DownloadProgress? ParseProgressLine(string line)
    {
        if (!line.Contains("[download]"))
            return null;

        var progress = new DownloadProgress { Status = "downloading" };

        var percentMatch = System.Text.RegularExpressions.Regex.Match(line, @"(\d+\.?\d*)%");
        if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, out var percent))
        {
            progress = progress with { Progress = percent };
        }

        var speedMatch = System.Text.RegularExpressions.Regex.Match(line, @"at\s+([\d.]+[KMGT]?i?B/s)");
        if (speedMatch.Success)
        {
            var speedStr = speedMatch.Groups[1].Value;
            progress = progress with { Speed = ParseSpeed(speedStr) };
        }

        var etaMatch = System.Text.RegularExpressions.Regex.Match(line, @"ETA\s+(\d+:\d+(?::\d+)?)");
        if (etaMatch.Success)
        {
            progress = progress with { Eta = ParseEta(etaMatch.Groups[1].Value) };
        }

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