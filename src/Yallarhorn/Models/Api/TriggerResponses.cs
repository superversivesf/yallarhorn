namespace Yallarhorn.Models.Api;

/// <summary>
/// Request model for channel refresh endpoint.
/// </summary>
public class RefreshChannelRequest
{
    /// <summary>
    /// Gets or sets whether to force refresh even if recently refreshed.
    /// </summary>
    public bool Force { get; set; } = false;
}

/// <summary>
/// Response model for channel refresh endpoint.
/// </summary>
public class RefreshChannelResponse
{
    /// <summary>
    /// Gets or sets the success message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets the channel ID.
    /// </summary>
    public required string ChannelId { get; set; }
}

/// <summary>
/// Response model for refresh all channels endpoint.
/// </summary>
public class RefreshAllChannelsResponse
{
    /// <summary>
    /// Gets or sets the success message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets the number of channels refreshed.
    /// </summary>
    public int ChannelsRefreshed { get; set; }
}

/// <summary>
/// Response model for retry episode endpoint.
/// </summary>
public class RetryEpisodeResponse
{
    /// <summary>
    /// Gets or sets the success message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets the episode ID.
    /// </summary>
    public required string EpisodeId { get; set; }

    /// <summary>
    /// Gets or sets the new status.
    /// </summary>
    public required string Status { get; set; }
}