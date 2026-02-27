using System.Text.Json.Serialization;

namespace Yallarhorn.Models.Api;

/// <summary>
/// Response model for health check endpoint.
/// </summary>
public class HealthStatus
{
    /// <summary>
    /// Gets or sets the health status ("healthy" or "unhealthy").
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the health check.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Response model for queue status endpoint.
/// </summary>
public class QueueStatusResponse
{
    /// <summary>
    /// Gets or sets the count of pending items.
    /// </summary>
    [JsonPropertyName("pending")]
    public int Pending { get; set; }

    /// <summary>
    /// Gets or sets the in-progress downloads with episode details.
    /// </summary>
    [JsonPropertyName("in_progress")]
    public List<InProgressDownload> InProgress { get; set; } = [];

    /// <summary>
    /// Gets or sets the recent failed downloads.
    /// </summary>
    [JsonPropertyName("failed")]
    public List<FailedDownload> Failed { get; set; } = [];
}

/// <summary>
/// Information about an in-progress download.
/// </summary>
public class InProgressDownload
{
    /// <summary>
    /// Gets or sets the episode ID.
    /// </summary>
    [JsonPropertyName("episode_id")]
    public required string EpisodeId { get; set; }

    /// <summary>
    /// Gets or sets the video ID.
    /// </summary>
    [JsonPropertyName("video_id")]
    public required string VideoId { get; set; }

    /// <summary>
    /// Gets or sets the episode title.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; set; }

    /// <summary>
    /// Gets or sets the channel title.
    /// </summary>
    [JsonPropertyName("channel_title")]
    public required string ChannelTitle { get; set; }

    /// <summary>
    /// Gets or sets the download attempts count.
    /// </summary>
    [JsonPropertyName("attempts")]
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets when the download started.
    /// </summary>
    [JsonPropertyName("started_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? StartedAt { get; set; }
}

/// <summary>
/// Information about a failed download.
/// </summary>
public class FailedDownload
{
    /// <summary>
    /// Gets or sets the episode ID.
    /// </summary>
    [JsonPropertyName("episode_id")]
    public required string EpisodeId { get; set; }

    /// <summary>
    /// Gets or sets the video ID.
    /// </summary>
    [JsonPropertyName("video_id")]
    public required string VideoId { get; set; }

    /// <summary>
    /// Gets or sets the episode title.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    [JsonPropertyName("error_message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the number of attempts.
    /// </summary>
    [JsonPropertyName("attempts")]
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets the maximum attempts allowed.
    /// </summary>
    [JsonPropertyName("max_attempts")]
    public int MaxAttempts { get; set; }

    /// <summary>
    /// Gets or sets when the download failed.
    /// </summary>
    [JsonPropertyName("failed_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? FailedAt { get; set; }
}