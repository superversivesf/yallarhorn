using System.Text.Json.Serialization;

namespace Yallarhorn.Models.Api;

/// <summary>
/// Generic wrapper for API responses with data.
/// </summary>
/// <typeparam name="T">The type of data being returned.</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Gets or sets the response data.
    /// </summary>
    [JsonPropertyName("data")]
    public required T Data { get; set; }
}

/// <summary>
/// Response model for system status endpoint.
/// </summary>
public class SystemStatus
{
    /// <summary>
    /// Gets or sets the application version.
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; set; }

    /// <summary>
    /// Gets or sets the application uptime in seconds.
    /// </summary>
    [JsonPropertyName("uptime_seconds")]
    public long UptimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets the storage information.
    /// </summary>
    [JsonPropertyName("storage")]
    public required StorageInfo Storage { get; set; }

    /// <summary>
    /// Gets or sets the queue information.
    /// </summary>
    [JsonPropertyName("queue")]
    public required QueueInfo Queue { get; set; }

    /// <summary>
    /// Gets or sets the download information.
    /// </summary>
    [JsonPropertyName("downloads")]
    public required DownloadInfo Downloads { get; set; }

    /// <summary>
    /// Gets or sets the last refresh timestamp.
    /// </summary>
    [JsonPropertyName("last_refresh")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? LastRefresh { get; set; }
}

/// <summary>
/// Storage information for system status.
/// </summary>
public class StorageInfo
{
    /// <summary>
    /// Gets or sets the used storage in bytes.
    /// </summary>
    [JsonPropertyName("used_bytes")]
    public long UsedBytes { get; set; }

    /// <summary>
    /// Gets or sets the free storage in bytes.
    /// </summary>
    [JsonPropertyName("free_bytes")]
    public long FreeBytes { get; set; }

    /// <summary>
    /// Gets or sets the total storage in bytes.
    /// </summary>
    [JsonPropertyName("total_bytes")]
    public long TotalBytes { get; set; }

    /// <summary>
    /// Gets or sets the storage usage percentage.
    /// </summary>
    [JsonPropertyName("used_percentage")]
    public double UsedPercentage { get; set; }
}

/// <summary>
/// Queue information for system status.
/// </summary>
public class QueueInfo
{
    /// <summary>
    /// Gets or sets the count of pending items.
    /// </summary>
    [JsonPropertyName("pending")]
    public int Pending { get; set; }

    /// <summary>
    /// Gets or sets the count of in-progress items.
    /// </summary>
    [JsonPropertyName("in_progress")]
    public int InProgress { get; set; }

    /// <summary>
    /// Gets or sets the count of completed items.
    /// </summary>
    [JsonPropertyName("completed")]
    public int Completed { get; set; }

    /// <summary>
    /// Gets or sets the count of failed items.
    /// </summary>
    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    /// <summary>
    /// Gets or sets the count of retrying items.
    /// </summary>
    [JsonPropertyName("retrying")]
    public int Retrying { get; set; }
}

/// <summary>
/// Download information for system status.
/// </summary>
public class DownloadInfo
{
    /// <summary>
    /// Gets or sets the count of active downloads.
    /// </summary>
    [JsonPropertyName("active")]
    public int Active { get; set; }

    /// <summary>
    /// Gets or sets the total count of completed downloads.
    /// </summary>
    [JsonPropertyName("completed_total")]
    public long CompletedTotal { get; set; }

    /// <summary>
    /// Gets or sets the total count of failed downloads.
    /// </summary>
    [JsonPropertyName("failed_total")]
    public long FailedTotal { get; set; }
}