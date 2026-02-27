using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Yallarhorn.Data.Enums;

namespace Yallarhorn.Data.Entities;

/// <summary>
/// Represents a download queue item with priority and retry logic.
/// </summary>
[Table("download_queue")]
public class DownloadQueue
{
    /// <summary>
    /// Unique identifier (UUID v4).
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(100)]
    public required string Id { get; set; }

    /// <summary>
    /// Foreign key to the episode being downloaded (unique, one-to-one).
    /// </summary>
    [Required]
    [Column("episode_id")]
    [MaxLength(100)]
    public required string EpisodeId { get; set; }

    /// <summary>
    /// Download priority (1-10, lower = higher priority).
    /// </summary>
    [Column("priority")]
    [Range(1, 10)]
    public int Priority { get; set; } = 5;

    /// <summary>
    /// Current queue item status.
    /// </summary>
    [Column("status")]
    public QueueStatus Status { get; set; } = QueueStatus.Pending;

    /// <summary>
    /// Number of download attempts made.
    /// </summary>
    [Column("attempts")]
    public int Attempts { get; set; } = 0;

    /// <summary>
    /// Maximum retries before marking as failed.
    /// </summary>
    [Column("max_attempts")]
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Last error message from failed attempt.
    /// </summary>
    [Column("last_error")]
    public string? LastError { get; set; }

    /// <summary>
    /// Scheduled time for next retry (exponential backoff).
    /// </summary>
    [Column("next_retry_at")]
    public DateTimeOffset? NextRetryAt { get; set; }

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
    /// Navigation property to the episode being downloaded.
    /// </summary>
    [ForeignKey(nameof(EpisodeId))]
    public virtual Episode? Episode { get; set; }
}