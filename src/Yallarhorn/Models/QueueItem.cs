namespace Yallarhorn.Models;

using Yallarhorn.Data.Enums;

/// <summary>
/// Represents a summary view of a download queue item.
/// </summary>
public class QueueItem
{
    /// <summary>
    /// Gets or sets the queue item ID.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the episode ID.
    /// </summary>
    public required string EpisodeId { get; set; }

    /// <summary>
    /// Gets or sets the download priority (1-10, lower = higher priority).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the current queue status.
    /// </summary>
    public QueueStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the number of attempts made.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed attempts.
    /// </summary>
    public int MaxAttempts { get; set; }

    /// <summary>
    /// Gets or sets the last error message.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets or sets the scheduled retry time.
    /// </summary>
    public DateTimeOffset? NextRetryAt { get; set; }

    /// <summary>
    /// Gets or sets when the item was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the item was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}