namespace Yallarhorn.Models;

/// <summary>
/// Represents an RSS 2.0 item/episode for podcast feeds.
/// </summary>
public class RssItem
{
    /// <summary>
    /// Gets or sets the episode title.
    /// Required RSS 2.0 element.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Gets or sets the episode link URL (typically the YouTube watch URL).
    /// Required RSS 2.0 element.
    /// </summary>
    public required string Link { get; set; }

    /// <summary>
    /// Gets or sets the episode description.
    /// Required RSS 2.0 element.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for the episode.
    /// Required RSS 2.0 element.
    /// </summary>
    public required string Guid { get; set; }

    /// <summary>
    /// Gets or sets whether the GUID is a permalink.
    /// Defaults to false as we use "yt:videoid" format.
    /// </summary>
    public bool IsPermaLink { get; set; } = false;

    /// <summary>
    /// Gets or sets the publication date.
    /// Required RSS 2.0 element.
    /// </summary>
    public DateTimeOffset? PubDate { get; set; }

    /// <summary>
    /// Gets or sets the enclosure information (media file).
    /// Required for podcast feeds.
    /// </summary>
    public RssEnclosure? Enclosure { get; set; }

    /// <summary>
    /// Gets or sets the episode duration in seconds.
    /// Optional iTunes podcast element.
    /// </summary>
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the episode thumbnail image URL.
    /// Optional iTunes podcast element.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the episode is explicit.
    /// Optional iTunes podcast element, defaults to false.
    /// </summary>
    public bool ITunesExplicit { get; set; } = false;

    /// <summary>
    /// Gets or sets the iTunes episode type (full, trailer, or bonus).
    /// Optional iTunes podcast element, defaults to "full".
    /// </summary>
    public string ITunesEpisodeType { get; set; } = "full";

    /// <summary>
    /// Gets or sets the episode number.
    /// Optional iTunes podcast element.
    /// </summary>
    public int? ITunesEpisode { get; set; }

    /// <summary>
    /// Gets or sets the season number.
    /// Optional iTunes podcast element.
    /// </summary>
    public int? ITunesSeason { get; set; }
}

/// <summary>
/// Represents an RSS enclosure element for media files.
/// </summary>
public class RssEnclosure
{
    /// <summary>
    /// Gets or sets the media file URL.
    /// Required attribute.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// Required attribute.
    /// </summary>
    public required long Length { get; set; }

    /// <summary>
    /// Gets or sets the MIME type of the file.
    /// Required attribute.
    /// </summary>
    public required string Type { get; set; }
}