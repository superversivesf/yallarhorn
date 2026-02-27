using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Yallarhorn.Models.Api;

/// <summary>
/// Request model for updating a channel. All fields are optional for partial updates.
/// </summary>
public class UpdateChannelRequest
{
    /// <summary>
    /// Gets or sets the channel title.
    /// </summary>
    [JsonPropertyName("title")]
    [MaxLength(500, ErrorMessage = "Title cannot exceed 500 characters")]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the number of episodes to keep (range: 1-1000).
    /// </summary>
    [JsonPropertyName("episode_count_config")]
    [Range(1, 1000, ErrorMessage = "Episode count config must be between 1 and 1000")]
    public int? EpisodeCountConfig { get; set; }

    /// <summary>
    /// Gets or sets the feed type (audio, video, both).
    /// </summary>
    [JsonPropertyName("feed_type")]
    [RegularExpression("^(audio|video|both)$", ErrorMessage = "Feed type must be 'audio', 'video', or 'both'")]
    public string? FeedType { get; set; }

    /// <summary>
    /// Gets or sets whether monitoring is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }
}