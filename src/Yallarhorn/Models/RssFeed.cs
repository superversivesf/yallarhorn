namespace Yallarhorn.Models;

/// <summary>
/// Represents an RSS 2.0 channel for podcast feeds.
/// </summary>
public class RssFeed
{
    /// <summary>
    /// Gets or sets the channel title.
    /// Required RSS 2.0 element.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Gets or sets the channel link URL (typically the source YouTube channel URL).
    /// Required RSS 2.0 element.
    /// </summary>
    public required string Link { get; set; }

    /// <summary>
    /// Gets or sets the channel description.
    /// Required RSS 2.0 element.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the channel language code.
    /// Optional RSS 2.0 element, defaults to "en-us".
    /// </summary>
    public string Language { get; set; } = "en-us";

    /// <summary>
    /// Gets or sets the channel thumbnail/image URL.
    /// Optional element, used for iTunes:image.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the last build date for the feed.
    /// Optional RSS 2.0 element.
    /// </summary>
    public DateTimeOffset? LastBuildDate { get; set; }

    /// <summary>
    /// Gets or sets the iTunes author name (typically same as Title).
    /// Optional iTunes podcast element.
    /// </summary>
    public string? ITunesAuthor { get; set; }

    /// <summary>
    /// Gets or sets the iTunes owner email.
    /// Optional iTunes podcast element.
    /// </summary>
    public string? ITunesOwnerEmail { get; set; }

    /// <summary>
    /// Gets or sets the iTunes category.
    /// Optional iTunes podcast element.
    /// </summary>
    public string? ITunesCategory { get; set; }

    /// <summary>
    /// Gets or sets whether the content is explicit.
    /// Optional iTunes podcast element, defaults to false.
    /// </summary>
    public bool ITunesExplicit { get; set; } = false;

    /// <summary>
    /// Gets or sets the iTunes podcast type (episodic or serial).
    /// Optional iTunes podcast element, defaults to "episodic".
    /// </summary>
    public string ITunesType { get; set; } = "episodic";

    /// <summary>
    /// Gets or sets the feed items/episodes.
    /// </summary>
    public List<RssItem> Items { get; set; } = new();
}