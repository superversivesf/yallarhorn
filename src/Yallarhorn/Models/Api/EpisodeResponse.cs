using System.Text.Json.Serialization;

namespace Yallarhorn.Models.Api;

/// <summary>
/// Represents an episode in API responses.
/// </summary>
public class EpisodeResponse
{
    /// <summary>
    /// Gets or sets the unique identifier of the episode.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the YouTube video ID.
    /// </summary>
    [JsonPropertyName("video_id")]
    public string VideoId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the channel ID this episode belongs to.
    /// </summary>
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the episode title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the episode description.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the episode thumbnail URL.
    /// </summary>
    [JsonPropertyName("thumbnail_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Gets or sets the video duration in seconds.
    /// </summary>
    [JsonPropertyName("duration_seconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the original YouTube publish timestamp.
    /// </summary>
    [JsonPropertyName("published_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>
    /// Gets or sets the download completion timestamp.
    /// </summary>
    [JsonPropertyName("downloaded_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? DownloadedAt { get; set; }

    /// <summary>
    /// Gets or sets the relative path to the audio file.
    /// </summary>
    [JsonPropertyName("file_path_audio")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FilePathAudio { get; set; }

    /// <summary>
    /// Gets or sets the relative path to the video file.
    /// </summary>
    [JsonPropertyName("file_path_video")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FilePathVideo { get; set; }

    /// <summary>
    /// Gets or sets the audio file size in bytes.
    /// </summary>
    [JsonPropertyName("file_size_audio")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? FileSizeAudio { get; set; }

    /// <summary>
    /// Gets or sets the video file size in bytes.
    /// </summary>
    [JsonPropertyName("file_size_video")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? FileSizeVideo { get; set; }

    /// <summary>
    /// Gets or sets the current download/processing status.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Gets or sets the number of failed download attempts.
    /// </summary>
    [JsonPropertyName("retry_count")]
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the last error message if status is 'failed'.
    /// </summary>
    [JsonPropertyName("error_message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the record creation timestamp.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the record last modification timestamp.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the HATEOAS links.
    /// </summary>
    [JsonPropertyName("_links")]
    public Dictionary<string, Link> Links { get; set; } = new();
}