namespace Yallarhorn.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents video metadata extracted from yt-dlp JSON output.
/// </summary>
public class YtDlpMetadata
{
    /// <summary>
    /// The video ID (e.g., YouTube video ID).
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The video title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The video description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Duration of the video in seconds.
    /// </summary>
    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    /// <summary>
    /// URL to the video thumbnail.
    /// </summary>
    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    /// <summary>
    /// The channel name.
    /// </summary>
    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    /// <summary>
    /// The channel ID.
    /// </summary>
    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; set; }

    /// <summary>
    /// The channel URL.
    /// </summary>
    [JsonPropertyName("channel_url")]
    public string? ChannelUrl { get; set; }

    /// <summary>
    /// Upload timestamp as Unix epoch seconds.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; set; }

    /// <summary>
    /// Upload date in YYYYMMDD format.
    /// </summary>
    [JsonPropertyName("upload_date")]
    public string? UploadDate { get; set; }

    /// <summary>
    /// The uploader name.
    /// </summary>
    [JsonPropertyName("uploader")]
    public string? Uploader { get; set; }

    /// <summary>
    /// The uploader ID.
    /// </summary>
    [JsonPropertyName("uploader_id")]
    public string? UploaderId { get; set; }

    /// <summary>
    /// View count.
    /// </summary>
    [JsonPropertyName("view_count")]
    public long? ViewCount { get; set; }

    /// <summary>
    /// Like count.
    /// </summary>
    [JsonPropertyName("like_count")]
    public long? LikeCount { get; set; }

    /// <summary>
    /// Video tags.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Video categories.
    /// </summary>
    [JsonPropertyName("categories")]
    public List<string>? Categories { get; set; }

    /// <summary>
    /// Video width in pixels.
    /// </summary>
    [JsonPropertyName("width")]
    public int? Width { get; set; }

    /// <summary>
    /// Video height in pixels.
    /// </summary>
    [JsonPropertyName("height")]
    public int? Height { get; set; }

    /// <summary>
    /// Resolution string (e.g., "1920x1080").
    /// </summary>
    [JsonPropertyName("resolution")]
    public string? Resolution { get; set; }

    /// <summary>
    /// Frames per second.
    /// </summary>
    [JsonPropertyName("fps")]
    public double? Fps { get; set; }

    /// <summary>
    /// Video codec.
    /// </summary>
    [JsonPropertyName("vcodec")]
    public string? Vcodec { get; set; }

    /// <summary>
    /// Audio codec.
    /// </summary>
    [JsonPropertyName("acodec")]
    public string? Acodec { get; set; }

    /// <summary>
    /// File extension.
    /// </summary>
    [JsonPropertyName("ext")]
    public string? Ext { get; set; }

    /// <summary>
    /// Estimated file size in bytes.
    /// </summary>
    [JsonPropertyName("filesize")]
    public long? Filesize { get; set; }

    /// <summary>
    /// The entry type (used in flat playlist output).
    /// </summary>
    [JsonPropertyName("_type")]
    public string? Type { get; set; }

    /// <summary>
    /// The extractor key (used in flat playlist output).
    /// </summary>
    [JsonPropertyName("ie_key")]
    public string? IeKey { get; set; }

    /// <summary>
    /// The URL (used in flat playlist output).
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// Converts the Unix timestamp to a DateTime if available.
    /// </summary>
    public DateTimeOffset? PublishedAt => Timestamp.HasValue
        ? DateTimeOffset.FromUnixTimeSeconds(Timestamp.Value)
        : null;
}

/// <summary>
/// Represents download progress information.
/// </summary>
public record DownloadProgress
{
    /// <summary>
    /// The download status (e.g., "downloading", "finished", "error").
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// The filename being downloaded.
    /// </summary>
    public string? Filename { get; init; }

    /// <summary>
    /// Bytes downloaded so far.
    /// </summary>
    public long? DownloadedBytes { get; init; }

    /// <summary>
    /// Total bytes to download.
    /// </summary>
    public long? TotalBytes { get; init; }

    /// <summary>
    /// Download speed in bytes per second.
    /// </summary>
    public double? Speed { get; init; }

    /// <summary>
    /// Download progress percentage (0-100).
    /// </summary>
    public double? Progress { get; init; }

    /// <summary>
    /// Estimated time remaining.
    /// </summary>
    public TimeSpan? Eta { get; init; }

    /// <summary>
    /// Whether the download is complete.
    /// </summary>
    public bool IsComplete => Status == "finished" || Status == "completed";
}