using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Yallarhorn.Models.Api;

/// <summary>
/// Request model for creating a new channel.
/// </summary>
public class CreateChannelRequest
{
    /// <summary>
    /// Gets or sets the YouTube channel URL (required).
    /// </summary>
    [JsonPropertyName("url")]
    [Required(ErrorMessage = "URL is required")]
    [Url(ErrorMessage = "Must be a valid URL")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional channel title override.
    /// </summary>
    [JsonPropertyName("title")]
    [MaxLength(500, ErrorMessage = "Title cannot exceed 500 characters")]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the optional description override.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the number of episodes to keep (default: 50, range: 1-1000).
    /// </summary>
    [JsonPropertyName("episode_count_config")]
    [Range(1, 1000, ErrorMessage = "Episode count config must be between 1 and 1000")]
    public int? EpisodeCountConfig { get; set; }

    /// <summary>
    /// Gets or sets the feed type (audio, video, both). Default: audio.
    /// </summary>
    [JsonPropertyName("feed_type")]
    [RegularExpression("^(audio|video|both)$", ErrorMessage = "Feed type must be 'audio', 'video', or 'both'")]
    public string? FeedType { get; set; }

    /// <summary>
    /// Gets or sets whether monitoring is enabled (default: true).
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }
}