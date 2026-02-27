namespace Yallarhorn.Models;

/// <summary>
/// Represents an Atom 1.0 feed for podcast clients.
/// </summary>
public class AtomFeed
{
    /// <summary>
    /// Gets or sets the unique identifier for the feed (typically the feed URL).
    /// Required Atom 1.0 element.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the feed title.
    /// Required Atom 1.0 element.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Gets or sets the feed subtitle/description.
    /// Optional Atom 1.0 element.
    /// </summary>
    public string? Subtitle { get; set; }

    /// <summary>
    /// Gets or sets the self link URL (the feed URL itself).
    /// Required: link with rel="self"
    /// </summary>
    public required string SelfLink { get; set; }

    /// <summary>
    /// Gets or sets the alternate link URL (original source URL).
    /// Required: link with rel="alternate" or href without rel.
    /// </summary>
    public required string AlternateLink { get; set; }

    /// <summary>
    /// Gets or sets the last updated timestamp.
    /// Required Atom 1.0 element.
    /// </summary>
    public DateTimeOffset Updated { get; set; }

    /// <summary>
    /// Gets or sets the author name.
    /// Required Atom 1.0 element (author/name).
    /// </summary>
    public required string AuthorName { get; set; }

    /// <summary>
    /// Gets or sets the feed entries/items.
    /// </summary>
    public List<AtomEntry> Entries { get; set; } = [];
}