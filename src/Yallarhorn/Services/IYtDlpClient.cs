namespace Yallarhorn.Services;

using Yallarhorn.Models;

/// <summary>
/// Interface for yt-dlp process execution operations.
/// </summary>
public interface IYtDlpClient
{
    /// <summary>
    /// Extracts video metadata without downloading.
    /// </summary>
    /// <param name="url">The video URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Video metadata.</returns>
    /// <exception cref="YtDlpException">Thrown when yt-dlp fails.</exception>
    Task<YtDlpMetadata> GetVideoMetadataAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of videos from a channel/playlist.
    /// </summary>
    /// <param name="channelUrl">The channel or playlist URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of video metadata.</returns>
    /// <exception cref="YtDlpException">Thrown when yt-dlp fails.</exception>
    Task<IReadOnlyList<YtDlpMetadata>> GetChannelVideosAsync(string channelUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a video with progress reporting.
    /// </summary>
    /// <param name="url">The video URL.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="progressCallback">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The path to the downloaded file.</returns>
    /// <exception cref="YtDlpException">Thrown when download fails.</exception>
    Task<string> DownloadVideoAsync(
        string url,
        string outputPath,
        Action<DownloadProgress>? progressCallback = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Exception thrown when yt-dlp operations fail.
/// </summary>
public class YtDlpException : Exception
{
    /// <summary>
    /// The exit code from the yt-dlp process.
    /// </summary>
    public int? ExitCode { get; }

    /// <summary>
    /// The stderr output from yt-dlp.
    /// </summary>
    public string? ErrorOutput { get; }

    /// <summary>
    /// Creates a new YtDlpException with a message.
    /// </summary>
    public YtDlpException(string message) : base(message) { }

    /// <summary>
    /// Creates a new YtDlpException with a message and inner exception.
    /// </summary>
    public YtDlpException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    /// Creates a new YtDlpException with exit code and error output.
    /// </summary>
    public YtDlpException(string message, int exitCode, string? errorOutput = null)
        : base($"{message} (ExitCode: {exitCode})")
    {
        ExitCode = exitCode;
        ErrorOutput = errorOutput;
    }
}