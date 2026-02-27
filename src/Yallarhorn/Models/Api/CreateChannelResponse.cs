using System.Text.Json.Serialization;

namespace Yallarhorn.Models.Api;

/// <summary>
/// Response model for creating a channel.
/// </summary>
public class CreateChannelResponse
{
    /// <summary>
    /// Gets or sets the created channel data.
    /// </summary>
    [JsonPropertyName("data")]
    public ChannelResponse Data { get; set; } = null!;

    /// <summary>
    /// Gets or sets the success message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}