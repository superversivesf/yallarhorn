namespace Yallarhorn.Configuration;

/// <summary>
/// Configuration options for yt-dlp.
/// </summary>
public class YtdlpOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Ytdlp";

    /// <summary>
    /// Path to cookies.txt file for YouTube authentication.
    /// Optional - used when YouTube requires sign-in verification.
    /// Export cookies from your browser after logging into YouTube.
    /// </summary>
    public string? CookiesPath { get; set; }

    /// <summary>
    /// HTTP proxy URL for routing requests through a proxy server.
    /// Helps avoid bot detection by appearing from different IPs.
    /// Example: "http://proxy.example.com:8080" or "socks5://127.0.0.1:1080"
    /// </summary>
    public string? ProxyUrl { get; set; }

    /// <summary>
    /// Minimum delay in seconds between yt-dlp API calls.
    /// Used to avoid rate limiting and bot detection.
    /// A random jitter is added to this value.
    /// Default: 2 seconds
    /// </summary>
    public int MinRequestDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Maximum delay in seconds between yt-dlp API calls.
    /// Actual delay is randomized between MinRequestDelaySeconds and this value.
    /// Default: 5 seconds
    /// </summary>
    public int MaxRequestDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Enable exponential backoff on rate limit errors (HTTP 429).
    /// When enabled, delays double after each retry up to MaxBackoffSeconds.
    /// Default: true
    /// </summary>
    public bool EnableExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Maximum backoff delay in seconds during exponential backoff.
    /// Default: 300 seconds (5 minutes)
    /// </summary>
    public int MaxBackoffSeconds { get; set; } = 300;

    /// <summary>
    /// Number of retries when encountering rate limits or transient errors.
    /// Default: 3
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}