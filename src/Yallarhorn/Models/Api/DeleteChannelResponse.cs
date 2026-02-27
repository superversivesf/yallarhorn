using System.Text.Json.Serialization;

namespace Yallarhorn.Models.Api;

/// <summary>
/// Represents the response for a channel deletion operation.
/// </summary>
public class DeleteChannelResponse
{
    /// <summary>
    /// Gets or sets the success message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = "Channel deleted successfully";

    /// <summary>
    /// Gets or sets the ID of the deleted channel.
    /// </summary>
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of episodes that were deleted.
    /// </summary>
    [JsonPropertyName("episodes_deleted")]
    public int EpisodesDeleted { get; set; }

    /// <summary>
    /// Gets or sets the number of files that were deleted from disk.
    /// </summary>
    [JsonPropertyName("files_deleted")]
    public int FilesDeleted { get; set; }

    /// <summary>
    /// Gets or sets the total bytes freed from disk.
    /// </summary>
    [JsonPropertyName("bytes_freed")]
    public long BytesFreed { get; set; }

    /// <summary>
    /// Gets the deleted property for backward compatibility with tests.
    /// This returns self for compatibility with the nested response structure.
    /// </summary>
    [JsonIgnore]
    public DeleteChannelResponse Deleted => this;
}