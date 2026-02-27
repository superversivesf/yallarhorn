using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Yallarhorn.Data.Enums;

namespace Yallarhorn.Data.Entities;

/// <summary>
/// Represents a downloaded video/episode with metadata.
/// </summary>
[Table("episodes")]
public class Episode
{
    /// <summary>
    /// Unique identifier (UUID v4).
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(100)]
    public required string Id { get; set; }

    /// <summary>
    /// YouTube video ID (unique, used for deduplication).
    /// </summary>
    [Required]
    [Column("video_id")]
    [MaxLength(50)]
    public required string VideoId { get; set; }

    /// <summary>
    /// Foreign key to the channel this episode belongs to.
    /// </summary>
    [Required]
    [Column("channel_id")]
    [MaxLength(100)]
    public required string ChannelId { get; set; }

    /// <summary>
    /// Episode title.
    /// </summary>
    [Required]
    [Column("title")]
    [MaxLength(500)]
    public required string Title { get; set; }

    /// <summary>
    /// Episode description.
    /// </summary>
    [Column("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Episode thumbnail URL.
    /// </summary>
    [Column("thumbnail_url")]
    [MaxLength(1000)]
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Video duration in seconds.
    /// </summary>
    [Column("duration_seconds")]
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// Original YouTube publish timestamp.
    /// </summary>
    [Column("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>
    /// When the download completed.
    /// </summary>
    [Column("downloaded_at")]
    public DateTimeOffset? DownloadedAt { get; set; }

    /// <summary>
    /// Relative path to transcoded audio file.
    /// </summary>
    [Column("file_path_audio")]
    [MaxLength(500)]
    public string? FilePathAudio { get; set; }

    /// <summary>
    /// Relative path to transcoded video file.
    /// </summary>
    [Column("file_path_video")]
    [MaxLength(500)]
    public string? FilePathVideo { get; set; }

    /// <summary>
    /// Audio file size in bytes (for RSS enclosure).
    /// </summary>
    [Column("file_size_audio")]
    public long? FileSizeAudio { get; set; }

    /// <summary>
    /// Video file size in bytes (for RSS enclosure).
    /// </summary>
    [Column("file_size_video")]
    public long? FileSizeVideo { get; set; }

    /// <summary>
    /// Current download/processing status.
    /// </summary>
    [Column("status")]
    public EpisodeStatus Status { get; set; } = EpisodeStatus.Pending;

    /// <summary>
    /// Number of failed download attempts.
    /// </summary>
    [Column("retry_count")]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Last error message if status is 'failed'.
    /// </summary>
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

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
    /// Navigation property to the channel this episode belongs to.
    /// </summary>
    [ForeignKey(nameof(ChannelId))]
    public virtual Channel? Channel { get; set; }

    /// <summary>
    /// Navigation property to the download queue entry for this episode.
    /// </summary>
    public virtual DownloadQueue? DownloadQueue { get; set; }
}