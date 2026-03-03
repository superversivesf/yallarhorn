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
}