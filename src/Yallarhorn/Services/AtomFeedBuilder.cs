namespace Yallarhorn.Services;

using System.Text;
using System.Xml;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;

/// <summary>
/// Builds Atom 1.0 compliant feeds with podcast enclosure support.
/// </summary>
public class AtomFeedBuilder : IAtomFeedBuilder
{
    private const string AtomNamespace = "http://www.w3.org/2005/Atom";

    /// <summary>
    /// MIME type mappings for common audio and video formats.
    /// </summary>
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".mp3", "audio/mpeg" },
        { ".m4a", "audio/mp4" },
        { ".aac", "audio/aac" },
        { ".ogg", "audio/ogg" },
        { ".mp4", "video/mp4" },
        { ".m4v", "video/mp4" },
        { ".webm", "video/webm" }
    };

    /// <inheritdoc />
    public string BuildAtomFeed(Channel channel, IEnumerable<Episode> episodes, FeedType feedType, string baseUrl, string feedPath, string? feedUrl = null)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(episodes);

        // UTF-8 without BOM for clean XML declaration
        var utf8NoBom = new UTF8Encoding(false);

        var settings = new XmlWriterSettings
        {
            Encoding = utf8NoBom,
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = false
        };

        // Use a memory stream with UTF-8 encoding for proper output
        using var memoryStream = new MemoryStream();
        using (var streamWriter = new StreamWriter(memoryStream, utf8NoBom, leaveOpen: true))
        using (var writer = XmlWriter.Create(streamWriter, settings))
        {
            WriteFeed(writer, channel, episodes, feedType, baseUrl, feedPath, feedUrl);
        }

        return utf8NoBom.GetString(memoryStream.ToArray());
    }

    private void WriteFeed(XmlWriter writer, Channel channel, IEnumerable<Episode> episodes, FeedType feedType, string baseUrl, string feedPath, string? feedUrl)
    {
        writer.WriteStartDocument();

        // Atom feed root element with namespace
        writer.WriteStartElement("feed", AtomNamespace);

        // Feed ID - use feedUrl if provided, otherwise construct from channel
        var feedId = feedUrl ?? $"{baseUrl}/feed/{channel.Id}/atom.xml";
        writer.WriteElementString("id", AtomNamespace, feedId);

        // Feed title
        writer.WriteElementString("title", AtomNamespace, channel.Title);

        // Feed subtitle (description)
        writer.WriteElementString("subtitle", AtomNamespace, channel.Description ?? string.Empty);

        // Link to the original source (YouTube channel)
        writer.WriteStartElement("link", AtomNamespace);
        writer.WriteAttributeString("href", channel.Url);
        writer.WriteEndElement();

        // Self link
        writer.WriteStartElement("link", AtomNamespace);
        writer.WriteAttributeString("rel", "self");
        writer.WriteAttributeString("href", feedId);
        writer.WriteEndElement();

        // Updated timestamp (use channel updated time)
        writer.WriteElementString("updated", AtomNamespace, FormatTimestamp(channel.UpdatedAt));

        // Author element
        writer.WriteStartElement("author", AtomNamespace);
        writer.WriteElementString("name", AtomNamespace, channel.Title);
        writer.WriteEndElement();

        // Write entries (episodes)
        var filteredEpisodes = GetFilteredEpisodes(episodes, feedType)
            .OrderByDescending(e => e.PublishedAt ?? DateTimeOffset.MinValue);

        foreach (var episode in filteredEpisodes)
        {
            WriteEntry(writer, channel, episode, feedType, baseUrl, feedPath);
        }

        writer.WriteEndElement(); // feed
        writer.WriteEndDocument();
    }

    private static IEnumerable<Episode> GetFilteredEpisodes(IEnumerable<Episode> episodes, FeedType feedType)
    {
        return feedType switch
        {
            FeedType.Audio => episodes.Where(e => !string.IsNullOrEmpty(e.FilePathAudio) && e.FileSizeAudio.HasValue),
            FeedType.Video => episodes.Where(e => !string.IsNullOrEmpty(e.FilePathVideo) && e.FileSizeVideo.HasValue),
            FeedType.Both => episodes.Where(e =>
                (!string.IsNullOrEmpty(e.FilePathAudio) && e.FileSizeAudio.HasValue) ||
                (!string.IsNullOrEmpty(e.FilePathVideo) && e.FileSizeVideo.HasValue)),
            _ => episodes
        };
    }

    private void WriteEntry(XmlWriter writer, Channel channel, Episode episode, FeedType feedType, string baseUrl, string feedPath)
    {
        writer.WriteStartElement("entry", AtomNamespace);

        // Entry ID (yt:videoId format)
        writer.WriteElementString("id", AtomNamespace, $"yt:{episode.VideoId}");

        // Title
        writer.WriteElementString("title", AtomNamespace, episode.Title);

        // Link to the episode (YouTube watch URL)
        writer.WriteStartElement("link", AtomNamespace);
        writer.WriteAttributeString("href", $"https://www.youtube.com/watch?v={episode.VideoId}");
        writer.WriteEndElement();

        // Summary (description)
        writer.WriteElementString("summary", AtomNamespace, episode.Description ?? string.Empty);

        // Content with HTML type (in CDATA)
        writer.WriteStartElement("content", AtomNamespace);
        writer.WriteAttributeString("type", "html");
        writer.WriteCData(episode.Description ?? string.Empty);
        writer.WriteEndElement();

        // Published timestamp
        if (episode.PublishedAt.HasValue)
        {
            writer.WriteElementString("published", AtomNamespace, FormatTimestamp(episode.PublishedAt.Value));
        }

        // Updated timestamp (use UpdatedAt if available, otherwise PublishedAt or CreatedAt)
        var entryUpdated = episode.UpdatedAt;
        if (episode.PublishedAt.HasValue)
        {
            entryUpdated = episode.PublishedAt.Value > entryUpdated ? episode.PublishedAt.Value : entryUpdated;
        }
        writer.WriteElementString("updated", AtomNamespace, FormatTimestamp(entryUpdated));

        // Enclosure link (media file)
        var enclosure = GetEnclosure(episode, feedType, baseUrl, feedPath);
        if (enclosure != null)
        {
            writer.WriteStartElement("link", AtomNamespace);
            writer.WriteAttributeString("rel", "enclosure");
            writer.WriteAttributeString("href", enclosure.Url);
            writer.WriteAttributeString("length", enclosure.Length.ToString());
            writer.WriteAttributeString("type", enclosure.Type);
            writer.WriteAttributeString("title", "Audio Download");
            writer.WriteEndElement();
        }

        writer.WriteEndElement(); // entry
    }

    private static EnclosureInfo? GetEnclosure(Episode episode, FeedType feedType, string baseUrl, string feedPath)
    {
        // Build the base URL including feedPath
        var baseMediaUrl = string.IsNullOrEmpty(feedPath)
            ? baseUrl.TrimEnd('/')
            : $"{baseUrl.TrimEnd('/')}/{feedPath.TrimStart('/').TrimEnd('/')}";

        // For audio feeds, use audio file
        if (feedType == FeedType.Audio || feedType == FeedType.Both)
        {
            if (!string.IsNullOrEmpty(episode.FilePathAudio) && episode.FileSizeAudio.HasValue)
            {
                return new EnclosureInfo
                {
                    Url = $"{baseMediaUrl}/{episode.FilePathAudio.TrimStart('/')}",
                    Length = episode.FileSizeAudio.Value,
                    Type = GetMimeType(episode.FilePathAudio)
                };
            }
        }

        // For video feeds, use video file
        if (feedType == FeedType.Video)
        {
            if (!string.IsNullOrEmpty(episode.FilePathVideo) && episode.FileSizeVideo.HasValue)
            {
                return new EnclosureInfo
                {
                    Url = $"{baseMediaUrl}/{episode.FilePathVideo.TrimStart('/')}",
                    Length = episode.FileSizeVideo.Value,
                    Type = GetMimeType(episode.FilePathVideo)
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the MIME type for a file based on its extension.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The MIME type string.</returns>
    private static string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return MimeTypes.TryGetValue(extension, out var mimeType)
            ? mimeType
            : "application/octet-stream";
    }

    /// <summary>
    /// Formats a DateTimeOffset to ISO 8601 UTC format with Z suffix.
    /// </summary>
    /// <param name="timestamp">The timestamp to format.</param>
    /// <returns>Formatted timestamp string in ISO 8601 UTC format.</returns>
    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        // Convert to UTC and format as ISO 8601 with Z suffix
        return timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }

    /// <summary>
    /// Represents enclosure information for media files.
    /// </summary>
    private class EnclosureInfo
    {
        public required string Url { get; set; }
        public required long Length { get; set; }
        public required string Type { get; set; }
    }
}