using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Yallarhorn.Data.Enums;

namespace Yallarhorn.Data.Entities;

/// <summary>
/// Represents a YouTube channel being monitored for new content.
/// </summary>
[Table("channels")]
public class Channel
{
    /// <summary>
    /// Unique identifier (UUID v4 or slug-based).
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(100)]
    public required string Id { get; set; }

    /// <summary>
    /// YouTube channel URL (must be unique).
    /// </summary>
    [Required]
    [Column("url")]
    [MaxLength(500)]
    public required string Url { get; set; }

    /// <summary>
    /// Channel display name.
    /// </summary>
    [Required]
    [Column("title")]
    [MaxLength(500)]
    public required string Title { get; set; }

    /// <summary>
    /// Channel description.
    /// </summary>
    [Column("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Channel thumbnail image URL.
    /// </summary>
    [Column("thumbnail_url")]
    [MaxLength(1000)]
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Number of episodes to keep in the rolling window.
    /// </summary>
    [Column("episode_count_config")]
    public int EpisodeCountConfig { get; set; } = 50;

    /// <summary>
    /// Feed output type.
    /// </summary>
    [Column("feed_type")]
    public FeedType FeedType { get; set; } = FeedType.Audio;

    /// <summary>
    /// Whether the channel is active.
    /// </summary>
    [Column("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Timestamp of last successful channel refresh.
    /// </summary>
    [Column("last_refresh_at")]
    public DateTimeOffset? LastRefreshAt { get; set; }

    /// <summary>
    /// Record creation timestamp.
    /// </summary>
    [Required]
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Record last modification timestamp.
    /// </summary>
    [Required]
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property for episodes belonging to this channel.
    /// </summary>
    public virtual ICollection<Episode> Episodes { get; set; } = [];
}