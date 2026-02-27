namespace Yallarhorn.Models;

/// <summary>
/// Represents an Atom 1.0 entry/episode for podcast feeds.
/// </summary>
public class AtomEntry
{
    /// <summary>
    /// Gets or sets the unique identifier for the entry.
    /// Required Atom 1.0 element.
    /// Format: yt:{video_id}
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the entry title.
    /// Required Atom 1.0 element.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Gets or sets the episode link URL (typically the YouTube watch URL).
    /// Required Atom 1.0 element.
    /// </summary>
    public required string Link { get; set; }

    /// <summary>
    /// Gets or sets the entry summary/description.
    /// Optional Atom 1.0 element (plain text).
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Gets or sets the entry content.
    /// Optional Atom 1.0 element (can be HTML with type="html").
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Gets or sets the publication date.
    /// Optional Atom 1.0 element.
    /// </summary>
    public DateTimeOffset? Published { get; set; }

    /// <summary>
    /// Gets or sets the last updated/modification date.
    /// Required Atom 1.0 element.
    /// </summary>
    public DateTimeOffset Updated { get; set; }

    /// <summary>
    /// Gets or sets the enclosure information (media file).
    /// Optional Atom 1.0 element via link element with rel="enclosure".
    /// </summary>
    public AtomEnclosure? Enclosure { get; set; }
}

/// <summary>
/// Represents an Atom enclosure element for media files.
/// </summary>
public class AtomEnclosure
{
    /// <summary>
    /// Gets or sets the media file URL.
    /// Required attribute (href).
    /// </summary>
    public required string Href { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// Required attribute (length).
    /// </summary>
    public required long Length { get; set; }

    /// <summary>
    /// Gets or sets the MIME type of the file.
    /// Required attribute (type).
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Gets or sets the title for the enclosure link.
    /// Optional attribute.
    /// </summary>
    public string? Title { get; set; }
}