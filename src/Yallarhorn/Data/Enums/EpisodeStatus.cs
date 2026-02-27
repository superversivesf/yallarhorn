namespace Yallarhorn.Data.Enums;

/// <summary>
/// Represents the download/processing status of an episode.
/// </summary>
public enum EpisodeStatus
{
    /// <summary>
    /// Episode queued for download, not yet started.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Currently being downloaded by yt-dlp.
    /// </summary>
    Downloading = 1,

    /// <summary>
    /// Download complete, being transcoded by ffmpeg.
    /// </summary>
    Processing = 2,

    /// <summary>
    /// Fully processed and available in feeds.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Download failed after max retries.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Episode removed from disk (outside rolling window).
    /// </summary>
    Deleted = 5
}