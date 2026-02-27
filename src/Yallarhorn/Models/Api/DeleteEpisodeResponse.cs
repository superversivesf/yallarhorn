using System.Text.Json.Serialization;

namespace Yallarhorn.Models.Api;

/// <summary>
/// Represents the response for an episode deletion operation.
/// </summary>
public class DeleteEpisodeResponse
{
    /// <summary>
    /// Gets or sets the ID of the deleted episode.
    /// </summary>
    [JsonPropertyName("episode_id")]
    public string EpisodeId { get; set; } = string.Empty;

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
}