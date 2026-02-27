namespace Yallarhorn.Services;

using System.Text;
using System.Xml;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Models;

/// <summary>
/// Builds RSS 2.0 compliant podcast feeds with iTunes namespace support.
/// </summary>
public class RssFeedBuilder : IRssFeedBuilder
{
    private const string ITunesNamespace = "http://www.itunes.com/dtds/podcast-1.0.dtd";
    private const string ContentNamespace = "http://purl.org/rss/1.0/modules/content/";

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
    public string BuildRssFeed(Channel channel, IEnumerable<Episode> episodes, FeedType feedType, string baseUrl, string feedPath)
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
            WriteFeed(writer, channel, episodes, feedType, baseUrl, feedPath);
        }

        return utf8NoBom.GetString(memoryStream.ToArray());
    }

    private void WriteFeed(XmlWriter writer, Channel channel, IEnumerable<Episode> episodes, FeedType feedType, string baseUrl, string feedPath)
    {
        writer.WriteStartDocument();

        // RSS root element with namespaces
        writer.WriteStartElement("rss");
        writer.WriteAttributeString("version", "2.0");
        writer.WriteAttributeString("xmlns", "itunes", null, ITunesNamespace);
        writer.WriteAttributeString("xmlns", "content", null, ContentNamespace);

        // Channel element
        writer.WriteStartElement("channel");

        // Required channel elements
        WriteElementWithEscaping(writer, "title", channel.Title);
        WriteElementWithEscaping(writer, "link", channel.Url);
        WriteElementWithEscaping(writer, "description", channel.Description ?? string.Empty);

        // Optional channel elements
        writer.WriteElementString("language", "en-us");

        // iTunes channel elements
        WriteITunesElement(writer, "type", "episodic");
        WriteITunesElement(writer, "author", channel.Title);
        WriteITunesElement(writer, "summary", channel.Description ?? string.Empty);
        WriteITunesElement(writer, "explicit", "false");

        // iTunes owner
        writer.WriteStartElement("itunes", "owner", ITunesNamespace);
        WriteITunesElement(writer, "name", channel.Title);
        WriteITunesElement(writer, "email", $"contact@{SanitizeEmail(channel.Title)}.com");
        writer.WriteEndElement();

        // iTunes image
        if (!string.IsNullOrEmpty(channel.ThumbnailUrl))
        {
            writer.WriteStartElement("itunes", "image", ITunesNamespace);
            writer.WriteAttributeString("href", channel.ThumbnailUrl);
            writer.WriteEndElement();
        }

        // Write items (episodes)
        var filteredEpisodes = GetFilteredEpisodes(episodes, feedType)
            .OrderByDescending(e => e.PublishedAt ?? DateTimeOffset.MinValue);

        foreach (var episode in filteredEpisodes)
        {
            WriteItem(writer, channel, episode, feedType, baseUrl, feedPath);
        }

        writer.WriteEndElement(); // channel
        writer.WriteEndElement(); // rss
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

    private void WriteItem(XmlWriter writer, Channel channel, Episode episode, FeedType feedType, string baseUrl, string feedPath)
    {
        writer.WriteStartElement("item");

        // Required item elements
        WriteElementWithEscaping(writer, "title", episode.Title);
        writer.WriteElementString("link", $"https://www.youtube.com/watch?v={episode.VideoId}");
        WriteElementWithEscaping(writer, "description", episode.Description ?? string.Empty);

        // GUID (unique identifier)
        writer.WriteStartElement("guid");
        writer.WriteAttributeString("isPermaLink", "false");
        writer.WriteString($"yt:{episode.VideoId}");
        writer.WriteEndElement();

        // PubDate
        if (episode.PublishedAt.HasValue)
        {
            writer.WriteElementString("pubDate", episode.PublishedAt.Value.ToString("r"));
        }

        // Enclosure
        var enclosure = GetEnclosure(episode, feedType, baseUrl, feedPath);
        if (enclosure != null)
        {
            writer.WriteStartElement("enclosure");
            writer.WriteAttributeString("url", enclosure.Url);
            writer.WriteAttributeString("length", enclosure.Length.ToString());
            writer.WriteAttributeString("type", enclosure.Type);
            writer.WriteEndElement();
        }

        // iTunes item elements
        WriteITunesElement(writer, "title", episode.Title);
        WriteITunesElement(writer, "explicit", "false");
        WriteITunesElement(writer, "episodeType", "full");

        // Duration
        if (episode.DurationSeconds.HasValue)
        {
            WriteITunesElement(writer, "duration", FormatDuration(episode.DurationSeconds.Value));
        }

        // Image
        if (!string.IsNullOrEmpty(episode.ThumbnailUrl))
        {
            writer.WriteStartElement("itunes", "image", ITunesNamespace);
            writer.WriteAttributeString("href", episode.ThumbnailUrl);
            writer.WriteEndElement();
        }

        // Content:encoded (full HTML description in CDATA)
        writer.WriteStartElement("content", "encoded", ContentNamespace);
        writer.WriteCData(episode.Description ?? string.Empty);
        writer.WriteEndElement();

        writer.WriteEndElement(); // item
    }

    private static RssEnclosure? GetEnclosure(Episode episode, FeedType feedType, string baseUrl, string feedPath)
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
                return new RssEnclosure
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
                return new RssEnclosure
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
    /// Formats a duration in seconds to iTunes duration format (HH:MM:SS or MM:SS).
    /// </summary>
    /// <param name="seconds">Duration in seconds.</param>
    /// <returns>Formatted duration string.</returns>
    public static string FormatDuration(int seconds)
    {
        if (seconds < 0)
        {
            return "0:00";
        }

        var timeSpan = TimeSpan.FromSeconds(seconds);

        if (timeSpan.TotalHours >= 1)
        {
            return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }

        return $"{timeSpan.Minutes}:{timeSpan.Seconds:D2}";
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
    /// Writes an XML element with proper XML escaping for special characters.
    /// </summary>
    private static void WriteElementWithEscaping(XmlWriter writer, string localName, string value)
    {
        writer.WriteElementString(localName, value);
    }

    /// <summary>
    /// Writes an iTunes-namespaced element.
    /// </summary>
    private static void WriteITunesElement(XmlWriter writer, string localName, string value)
    {
        writer.WriteElementString("itunes", localName, ITunesNamespace, value);
    }

    /// <summary>
    /// Sanitizes a title for use in email addresses.
    /// </summary>
    private static string SanitizeEmail(string title)
    {
        return new string(title.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c)).ToArray());
    }
}