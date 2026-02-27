namespace Yallarhorn.Models;

/// <summary>
/// Result of a feed generation operation.
/// </summary>
public class FeedGenerationResult
{
    /// <summary>
    /// The generated XML content of the feed.
    /// </summary>
    public required string XmlContent { get; set; }

    /// <summary>
    /// ETag value (SHA256 hash) for cache validation.
    /// </summary>
    public required string Etag { get; set; }

    /// <summary>
    /// The timestamp when the feed was last modified.
    /// </summary>
    public DateTimeOffset LastModified { get; set; }
}