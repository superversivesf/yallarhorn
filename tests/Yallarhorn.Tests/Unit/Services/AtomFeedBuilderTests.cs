namespace Yallarhorn.Tests.Unit.Services;

using System.Xml.Linq;
using FluentAssertions;
using Xunit;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Services;

public class AtomFeedBuilderTests
{
    private const string BaseUrl = "http://localhost:8080";
    private const string FeedPath = "/feeds";
    private readonly IAtomFeedBuilder _builder;

    public AtomFeedBuilderTests()
    {
        _builder = new AtomFeedBuilder();
    }

    #region Basic Feed Structure Tests

    [Fact]
    public void BuildAtomFeed_WithValidChannelAndEpisodes_ShouldReturnValidXml()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var doc = XDocument.Parse(result);
        doc.Should().NotBeNull();
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.LocalName.Should().Be("feed");
    }

    [Fact]
    public void BuildAtomFeed_ShouldIncludeXmlDeclarationWithUtf8Encoding()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
    }

    [Fact]
    public void BuildAtomFeed_ShouldIncludeAtomNamespace()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("xmlns=\"http://www.w3.org/2005/Atom\"");
    }

    [Fact]
    public void BuildAtomFeed_ShouldBeProperlyIndented()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert - verify the feed structure has proper indentation
        result.Should().Contain("\n  <title>");
        result.Should().Contain("\n  <author>");
    }

    #endregion

    #region Required Feed Elements Tests

    [Fact]
    public void BuildAtomFeed_ShouldIncludeFeedId()
    {
        // Arrange
        var channel = CreateTestChannel(id: "ch-abc123");
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath, feedUrl: "http://localhost:8080/feed/ch-abc123/atom.xml");

        // Assert
        var doc = XDocument.Parse(result);
        // Use XNamespace for Atom namespace lookup
        var ns = XNamespace.Get("http://www.w3.org/2005/Atom");
        var idElement = doc.Root?.Element(ns + "id");
        idElement.Should().NotBeNull();
        idElement!.Value.Should().Be("http://localhost:8080/feed/ch-abc123/atom.xml");
    }

    [Fact]
    public void BuildAtomFeed_ShouldIncludeFeedTitle()
    {
        // Arrange
        var channel = CreateTestChannel(title: "Tech Talk Weekly");
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        var doc = XDocument.Parse(result);
        var ns = XNamespace.Get("http://www.w3.org/2005/Atom");
        var titleElement = doc.Root?.Element(ns + "title");
        titleElement.Should().NotBeNull();
        titleElement!.Value.Should().Be("Tech Talk Weekly");
    }

    [Fact]
    public void BuildAtomFeed_ShouldIncludeFeedSubtitle()
    {
        // Arrange
        var channel = CreateTestChannel(description: "Technology news and discussion");
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        var doc = XDocument.Parse(result);
        var ns = XNamespace.Get("http://www.w3.org/2005/Atom");
        var subtitleElement = doc.Root?.Element(ns + "subtitle");
        subtitleElement.Should().NotBeNull();
        subtitleElement!.Value.Should().Be("Technology news and discussion");
    }

    [Fact]
    public void BuildAtomFeed_ShouldIncludeSelfLink()
    {
        // Arrange
        var channel = CreateTestChannel(id: "ch-abc123");
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath, feedUrl: "http://localhost:8080/feed/ch-abc123/atom.xml");

        // Assert
        result.Should().Contain("<link rel=\"self\" href=\"http://localhost:8080/feed/ch-abc123/atom.xml\" />");
    }

    [Fact]
    public void BuildAtomFeed_ShouldIncludeAlternateLink()
    {
        // Arrange
        var channel = CreateTestChannel(url: "https://youtube.com/@techtalk");
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<link href=\"https://youtube.com/@techtalk\" />");
    }

    [Fact]
    public void BuildAtomFeed_ShouldIncludeUpdatedTimestamp()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        var doc = XDocument.Parse(result);
        var ns = XNamespace.Get("http://www.w3.org/2005/Atom");
        var updatedElement = doc.Root?.Element(ns + "updated");
        updatedElement.Should().NotBeNull();
        // Verify ISO 8601 format: yyyy-MM-ddTHH:mm:ssZ
        var updatedValue = updatedElement!.Value;
        updatedValue.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$");
    }

    [Fact]
    public void BuildAtomFeed_ShouldIncludeAuthor()
    {
        // Arrange
        var channel = CreateTestChannel(title: "Tech Talk Weekly");
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<author>");
        result.Should().Contain("<name>Tech Talk Weekly</name>");
        result.Should().Contain("</author>");
    }

    #endregion

    #region Entry Elements Tests

    [Fact]
    public void BuildAtomFeed_WithEpisode_ShouldIncludeEntryId()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, videoId: "abc123", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<id>yt:abc123</id>");
    }

    [Fact]
    public void BuildAtomFeed_WithEpisode_ShouldIncludeEntryTitle()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, title: "Episode 42: AI Revolution", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        var doc = XDocument.Parse(result);
        var ns = XNamespace.Get("http://www.w3.org/2005/Atom");
        var entry = doc.Root?.Elements(ns + "entry").First();
        var titleElement = entry?.Element(ns + "title");
        titleElement.Should().NotBeNull();
        titleElement!.Value.Should().Be("Episode 42: AI Revolution");
    }

    [Fact]
    public void BuildAtomFeed_WithEpisode_ShouldIncludeEntryLink()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, videoId: "abc123", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<link href=\"https://www.youtube.com/watch?v=abc123\" />");
    }

    [Fact]
    public void BuildAtomFeed_WithEpisode_ShouldIncludeEntrySummary()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, description: "Discussion about AI trends...", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<summary>Discussion about AI trends...</summary>");
    }

    [Fact]
    public void BuildAtomFeed_WithEpisode_ShouldIncludeContentWithHtmlType()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, description: "Full HTML description", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<content type=\"html\"><![CDATA[Full HTML description]]></content>");
    }

    [Fact]
    public void BuildAtomFeed_WithEpisode_ShouldIncludePublishedTimestamp()
    {
        // Arrange
        var channel = CreateTestChannel();
        var publishedAt = new DateTimeOffset(2024, 1, 15, 9, 0, 0, TimeSpan.Zero);
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, publishedAt: publishedAt, filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<published>2024-01-15T09:00:00Z</published>");
    }

    [Fact]
    public void BuildAtomFeed_WithEpisode_ShouldIncludeUpdatedTimestamp()
    {
        // Arrange
        var channel = CreateTestChannel();
        var updatedAt = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, updatedAt: updatedAt, filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        // Entry updated should use the episode's UpdatedAt timestamp
        result.Should().Contain("<updated>2024-01-15T10:30:00Z</updated>");
    }

    #endregion

    #region Enclosure Link Tests

    [Fact]
    public void BuildAtomFeed_AudioFeed_ShouldIncludeEnclosureLink()
    {
        // Arrange
        var channel = CreateTestChannel(id: "ch-abc123");
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel,
                videoId: "xyz789",
                filePathAudio: "tech-talk/audio/xyz789.mp3",
                fileSizeAudio: 52428800)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("rel=\"enclosure\"");
        result.Should().Contain("href=\"http://localhost:8080/feeds/tech-talk/audio/xyz789.mp3\"");
        result.Should().Contain("length=\"52428800\"");
        result.Should().Contain("type=\"audio/mpeg\"");
    }

    [Fact]
    public void BuildAtomFeed_VideoFeed_ShouldIncludeEnclosureLink()
    {
        // Arrange
        var channel = CreateTestChannel(id: "ch-abc123");
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel,
                videoId: "xyz789",
                filePathVideo: "tech-talk/video/xyz789.mp4",
                fileSizeVideo: 524288000)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Video, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("rel=\"enclosure\"");
        result.Should().Contain("href=\"http://localhost:8080/feeds/tech-talk/video/xyz789.mp4\"");
        result.Should().Contain("length=\"524288000\"");
        result.Should().Contain("type=\"video/mp4\"");
    }

    [Fact]
    public void BuildAtomFeed_EnclosureLink_ShouldHaveTitleAttribute()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, title: "Episode 42", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("title=\"Audio Download\"");
    }

    #endregion

    #region MIME Type Tests

    [Fact]
    public void BuildAtomFeed_Mp3File_ShouldHaveCorrectMimeType()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("type=\"audio/mpeg\"");
    }

    [Fact]
    public void BuildAtomFeed_M4aFile_ShouldHaveCorrectMimeType()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, filePathAudio: "test/audio.m4a", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("type=\"audio/mp4\"");
    }

    [Fact]
    public void BuildAtomFeed_Mp4File_ShouldHaveCorrectMimeType()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, filePathVideo: "test/video.mp4", fileSizeVideo: 1024)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Video, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("type=\"video/mp4\"");
    }

    #endregion

    #region Episode Filtering Tests

    [Fact]
    public void BuildAtomFeed_EpisodeWithoutAudioFile_ShouldBeSkippedForAudioFeed()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, title: "Has Audio", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024),
            CreateTestEpisode(channel, title: "No Audio", filePathAudio: null, fileSizeAudio: null)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<title>Has Audio</title>");
        result.Should().NotContain("<title>No Audio</title>");
    }

    [Fact]
    public void BuildAtomFeed_EpisodeWithoutVideoFile_ShouldBeSkippedForVideoFeed()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, title: "Has Video", filePathVideo: "test/video.mp4", fileSizeVideo: 1024),
            CreateTestEpisode(channel, title: "No Video", filePathVideo: null, fileSizeVideo: null)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Video, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<title>Has Video</title>");
        result.Should().NotContain("<title>No Video</title>");
    }

    #endregion

    #region Episode Ordering Tests

    [Fact]
    public void BuildAtomFeed_ShouldOrderEpisodesByPublishedAtDescending()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, title: "Old Episode", publishedAt: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), filePathAudio: "test/audio.mp3", fileSizeAudio: 1024),
            CreateTestEpisode(channel, title: "New Episode", publishedAt: new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero), filePathAudio: "test/audio.mp3", fileSizeAudio: 1024),
            CreateTestEpisode(channel, title: "Middle Episode", publishedAt: new DateTimeOffset(2024, 1, 8, 0, 0, 0, TimeSpan.Zero), filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        var doc = XDocument.Parse(result);
        var ns = XNamespace.Get("http://www.w3.org/2005/Atom");
        var entryTitles = doc.Root?.Elements(ns + "entry")
            .Select(e => e.Element(ns + "title")?.Value)
            .ToList();
        entryTitles.Should().HaveCount(3);
        entryTitles![0].Should().Be("New Episode");
        entryTitles[1].Should().Be("Middle Episode");
        entryTitles[2].Should().Be("Old Episode");
    }

    #endregion

    #region XML Escaping Tests

    [Fact]
    public void BuildAtomFeed_TitleWithSpecialCharacters_ShouldEscapeCorrectly()
    {
        // Arrange
        var channel = CreateTestChannel(title: "Tech & Talk <Podcast> \"Weekly\"");
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("Tech &amp; Talk &lt;Podcast&gt; \"Weekly\"");
    }

    [Fact]
    public void BuildAtomFeed_DescriptionWithXmlCharacters_ShouldEscapeCorrectly()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, description: "Description with <script>alert('xss')</script> & more", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("&lt;script&gt;alert('xss')&lt;/script&gt; &amp; more");
    }

    [Fact]
    public void BuildAtomFeed_DescriptionInContent_ShouldBeWrappedInCData()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, description: "Description with <html> tags & entities", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<content type=\"html\"><![CDATA[Description with <html> tags & entities]]></content>");
    }

    #endregion

    #region Empty Feed Tests

    [Fact]
    public void BuildAtomFeed_WithNoEpisodes_ShouldReturnValidEmptyFeed()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildAtomFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        var doc = XDocument.Parse(result);
        var ns = XNamespace.Get("http://www.w3.org/2005/Atom");
        var entries = doc.Root?.Elements(ns + "entry").ToList();
        entries.Should().BeEmpty();
    }

    #endregion

    #region Complete Feed Example Test

    [Fact]
    public void BuildAtomFeed_WithCompleteData_ShouldGenerateValidAtom10Feed()
    {
        // Arrange
        var channel = CreateTestChannel(
            id: "tech-talk-weekly",
            title: "Tech Talk Weekly",
            url: "https://youtube.com/@techtalk",
            description: "Technology news and discussion from industry experts.",
            thumbnailUrl: "http://localhost:8080/feeds/tech-talk/cover.jpg"
        );

        var episodes = new List<Episode>
        {
            CreateTestEpisode(
                channel,
                videoId: "abc123",
                title: "Episode 42: The AI Revolution",
                description: "An in-depth discussion about artificial intelligence trends.",
                durationSeconds: 5445, // 1:30:45
                publishedAt: new DateTimeOffset(2024, 1, 15, 9, 0, 0, TimeSpan.Zero),
                updatedAt: new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero),
                filePathAudio: "tech-talk/audio/abc123.mp3",
                fileSizeAudio: 52428800,
                thumbnailUrl: "http://localhost:8080/feeds/tech-talk/episodes/abc123-thumb.jpg"
            )
        };

        // Act
        var result = _builder.BuildAtomFeed(
            channel,
            episodes,
            FeedType.Audio,
            BaseUrl,
            FeedPath,
            feedUrl: "http://localhost:8080/feed/tech-talk-weekly/atom.xml"
        );

        // Assert - Verify it parses as valid XML
        var doc = XDocument.Parse(result);
        var ns = XNamespace.Get("http://www.w3.org/2005/Atom");
        doc.Should().NotBeNull();

        // Verify Atom namespace
        doc.Root?.Name.NamespaceName.Should().Be("http://www.w3.org/2005/Atom");

        // Verify feed elements
        var feedElement = doc.Root;
        feedElement.Should().NotBeNull();
        feedElement?.Element(ns + "id")?.Value.Should().Be("http://localhost:8080/feed/tech-talk-weekly/atom.xml");
        feedElement?.Element(ns + "title")?.Value.Should().Be("Tech Talk Weekly");
        feedElement?.Element(ns + "subtitle")?.Value.Should().Be("Technology news and discussion from industry experts.");

        // Verify entry elements
        var entry = feedElement?.Element(ns + "entry");
        entry.Should().NotBeNull();
        entry?.Element(ns + "id")?.Value.Should().Be("yt:abc123");
        entry?.Element(ns + "title")?.Value.Should().Be("Episode 42: The AI Revolution");
        entry?.Element(ns + "summary")?.Value.Should().Be("An in-depth discussion about artificial intelligence trends.");
        entry?.Element(ns + "published")?.Value.Should().Be("2024-01-15T09:00:00Z");
        entry?.Element(ns + "updated")?.Value.Should().Be("2024-01-15T10:30:00Z");

        // Verify enclosure link
        var links = entry?.Elements(ns + "link").ToList();
        links.Should().HaveCount(2);
        var enclosureLink = links?.FirstOrDefault(l => l.Attribute("rel")?.Value == "enclosure");
        enclosureLink.Should().NotBeNull();
        enclosureLink?.Attribute("href")?.Value.Should().Be("http://localhost:8080/feeds/tech-talk/audio/abc123.mp3");
        enclosureLink?.Attribute("length")?.Value.Should().Be("52428800");
        enclosureLink?.Attribute("type")?.Value.Should().Be("audio/mpeg");
    }

    #endregion

    #region Helper Methods

    private static Channel CreateTestChannel(
        string? id = null,
        string? title = null,
        string? url = null,
        string? description = null,
        string? thumbnailUrl = null)
    {
        return new Channel
        {
            Id = id ?? "test-channel-id",
            Title = title ?? "Test Channel",
            Url = url ?? "https://youtube.com/@test",
            Description = description ?? "Test channel description",
            ThumbnailUrl = thumbnailUrl,
            FeedType = FeedType.Audio,
            EpisodeCountConfig = 50,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static Episode CreateTestEpisode(
        Channel channel,
        string? videoId = null,
        string? title = null,
        string? description = null,
        int? durationSeconds = null,
        DateTimeOffset? publishedAt = null,
        DateTimeOffset? updatedAt = null,
        string? filePathAudio = null,
        long? fileSizeAudio = null,
        string? filePathVideo = null,
        long? fileSizeVideo = null,
        string? thumbnailUrl = null)
    {
        return new Episode
        {
            Id = Guid.NewGuid().ToString("N"),
            VideoId = videoId ?? "test-video-id",
            ChannelId = channel.Id,
            Channel = channel,
            Title = title ?? "Test Episode",
            Description = description ?? "Test episode description",
            ThumbnailUrl = thumbnailUrl,
            DurationSeconds = durationSeconds,
            PublishedAt = publishedAt,
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow,
            FilePathAudio = filePathAudio,
            FileSizeAudio = fileSizeAudio,
            FilePathVideo = filePathVideo,
            FileSizeVideo = fileSizeVideo,
            Status = EpisodeStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    #endregion
}