using System.Text.Json.Serialization;
using Yallarhorn.Models.Api;

namespace Yallarhorn.Models.Api;

/// <summary>
/// Represents a channel in API responses.
/// </summary>
public class ChannelResponse
{
    /// <summary>
    /// Gets or sets the unique identifier of the channel.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the YouTube channel URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the channel display name.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the channel description.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the channel thumbnail URL.
    /// </summary>
    [JsonPropertyName("thumbnail_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Gets or sets the episode count configuration.
    /// </summary>
    [JsonPropertyName("episode_count_config")]
    public int EpisodeCountConfig { get; set; }

    /// <summary>
    /// Gets or sets the feed type (audio, video, both).
    /// </summary>
    [JsonPropertyName("feed_type")]
    public string FeedType { get; set; } = "audio";

    /// <summary>
    /// Gets or sets whether the channel is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the computed episode count.
    /// </summary>
    [JsonPropertyName("episode_count")]
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of last successful refresh.
    /// </summary>
    [JsonPropertyName("last_refresh_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? LastRefreshAt { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the HATEOAS links.
    /// </summary>
    [JsonPropertyName("_links")]
    public Dictionary<string, Link> Links { get; set; } = new();
}