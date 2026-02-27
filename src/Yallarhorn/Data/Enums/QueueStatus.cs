namespace Yallarhorn.Data.Enums;

/// <summary>
/// Represents the status of a download queue item.
/// </summary>
public enum QueueStatus
{
    /// <summary>
    /// Item is waiting to be processed.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Item is currently being processed.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Item has been successfully processed.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Item processing has failed, waiting for retry.
    /// </summary>
    Retrying = 3,

    /// <summary>
    /// Item processing has failed permanently.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Item was cancelled before completion.
    /// </summary>
    Cancelled = 5
}