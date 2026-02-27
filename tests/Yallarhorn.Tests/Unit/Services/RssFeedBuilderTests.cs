namespace Yallarhorn.Tests.Unit.Services;

using System.Xml.Linq;
using FluentAssertions;
using Xunit;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Models;
using Yallarhorn.Services;

public class RssFeedBuilderTests
{
    private const string BaseUrl = "http://localhost:8080";
    private const string FeedPath = "/feeds";
    private readonly IRssFeedBuilder _builder;

    public RssFeedBuilderTests()
    {
        _builder = new RssFeedBuilder();
    }

    #region Basic Feed Structure Tests

    [Fact]
    public void BuildRssFeed_WithValidChannelAndEpisodes_ShouldReturnValidXml()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var doc = XDocument.Parse(result);
        doc.Should().NotBeNull();
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.LocalName.Should().Be("rss");
    }

    [Fact]
    public void BuildRssFeed_ShouldIncludeXmlDeclarationWithUtf8Encoding()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
    }

    [Fact]
    public void BuildRssFeed_ShouldIncludeRssVersion2_0()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("version=\"2.0\"");
    }

    [Fact]
    public void BuildRssFeed_ShouldIncludeITunesNamespace()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("xmlns:itunes=\"http://www.itunes.com/dtds/podcast-1.0.dtd\"");
    }

    [Fact]
    public void BuildRssFeed_ShouldIncludeContentNamespace()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("xmlns:content=\"http://purl.org/rss/1.0/modules/content/\"");
    }

    [Fact]
    public void BuildRssFeed_ShouldBeProperlyIndented()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("\n  <channel>");
        result.Should().Contain("\n    <title>");
    }

    #endregion

    #region Required Channel Elements Tests

    [Fact]
    public void BuildRssFeed_ShouldIncludeChannelTitle()
    {
        // Arrange
        var channel = CreateTestChannel(title: "Tech Talk Weekly");
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        var doc = XDocument.Parse(result);
        var titleElement = doc.Root?.Element("channel")?.Element("title");
        titleElement.Should().NotBeNull();
        titleElement!.Value.Should().Be("Tech Talk Weekly");
    }

    [Fact]
    public void BuildRssFeed_ShouldIncludeChannelLink()
    {
        // Arrange
        var channel = CreateTestChannel(url: "https://youtube.com/@techtalk");
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        var doc = XDocument.Parse(result);
        var linkElement = doc.Root?.Element("channel")?.Element("link");
        linkElement.Should().NotBeNull();
        linkElement!.Value.Should().Be("https://youtube.com/@techtalk");
    }

    [Fact]
    public void BuildRssFeed_ShouldIncludeChannelDescription()
    {
        // Arrange
        var channel = CreateTestChannel(description: "Technology news and discussion");
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        var doc = XDocument.Parse(result);
        var descElement = doc.Root?.Element("channel")?.Element("description");
        descElement.Should().NotBeNull();
        descElement!.Value.Should().Be("Technology news and discussion");
    }

    [Fact]
    public void BuildRssFeed_ShouldIncludeChannelLanguage()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        var doc = XDocument.Parse(result);
        var langElement = doc.Root?.Element("channel")?.Element("language");
        langElement.Should().NotBeNull();
        langElement!.Value.Should().Be("en-us");
    }

    #endregion

    #region iTunes Channel Elements Tests

    [Fact]
    public void BuildRssFeed_ShouldIncludeITunesAuthor()
    {
        // Arrange
        var channel = CreateTestChannel(title: "Tech Talk Weekly");
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<itunes:author>Tech Talk Weekly</itunes:author>");
    }

    [Fact]
    public void BuildRssFeed_ShouldIncludeITunesSummary()
    {
        // Arrange
        var channel = CreateTestChannel(description: "Technology news and discussion");
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<itunes:summary>Technology news and discussion</itunes:summary>");
    }

    [Fact]
    public void BuildRssFeed_ShouldIncludeITunesType()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<itunes:type>episodic</itunes:type>");
    }

    [Fact]
    public void BuildRssFeed_ShouldIncludeITunesExplicitFalse()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<itunes:explicit>false</itunes:explicit>");
    }

    [Fact]
    public void BuildRssFeed_ShouldIncludeITunesOwner()
    {
        // Arrange
        var channel = CreateTestChannel(title: "Tech Talk Weekly");
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<itunes:owner>");
        result.Should().Contain("<itunes:name>Tech Talk Weekly</itunes:name>");
        result.Should().Contain("</itunes:owner>");
    }

    [Fact]
    public void BuildRssFeed_WithThumbnail_ShouldIncludeITunesImage()
    {
        // Arrange
        var channel = CreateTestChannel(thumbnailUrl: "http://localhost:8080/feeds/tech-talk/cover.jpg");
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<itunes:image href=\"http://localhost:8080/feeds/tech-talk/cover.jpg\" />");
    }

    #endregion

    #region Required Item Elements Tests

    [Fact]
    public void BuildRssFeed_WithEpisode_ShouldIncludeItemTitle()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, title: "Episode 42: AI Revolution", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        var doc = XDocument.Parse(result);
        var itemTitle = doc.Root?.Element("channel")?.Elements("item").First().Element("title");
        itemTitle.Should().NotBeNull();
        itemTitle!.Value.Should().Be("Episode 42: AI Revolution");
    }

    [Fact]
    public void BuildRssFeed_WithEpisode_ShouldIncludeItemLink()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, videoId: "abc123", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<link>https://www.youtube.com/watch?v=abc123</link>");
    }

    [Fact]
    public void BuildRssFeed_WithEpisode_ShouldIncludeItemDescription()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, description: "Discussion about AI trends...", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        var doc = XDocument.Parse(result);
        var itemDesc = doc.Root?.Element("channel")?.Elements("item").First().Element("description");
        itemDesc.Should().NotBeNull();
        itemDesc!.Value.Should().Be("Discussion about AI trends...");
    }

    [Fact]
    public void BuildRssFeed_WithEpisode_ShouldIncludeGuid()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, videoId: "abc123", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<guid isPermaLink=\"false\">yt:abc123</guid>");
    }

    [Fact]
    public void BuildRssFeed_WithEpisode_ShouldIncludePubDate()
    {
        // Arrange
        var channel = CreateTestChannel();
        var publishedAt = new DateTimeOffset(2024, 1, 15, 9, 0, 0, TimeSpan.Zero);
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, publishedAt: publishedAt, filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<pubDate>Mon, 15 Jan 2024 09:00:00 GMT</pubDate>");
    }

    #endregion

    #region Enclosure Tag Tests

    [Fact]
    public void BuildRssFeed_AudioFeed_ShouldIncludeAudioEnclosure()
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
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<enclosure");
        result.Should().Contain("url=\"http://localhost:8080/feeds/tech-talk/audio/xyz789.mp3\"");
        result.Should().Contain("length=\"52428800\"");
        result.Should().Contain("type=\"audio/mpeg\"");
    }

    [Fact]
    public void BuildRssFeed_VideoFeed_ShouldIncludeVideoEnclosure()
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
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Video, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<enclosure");
        result.Should().Contain("url=\"http://localhost:8080/feeds/tech-talk/video/xyz789.mp4\"");
        result.Should().Contain("length=\"524288000\"");
        result.Should().Contain("type=\"video/mp4\"");
    }

    [Fact]
    public void BuildRssFeed_Mp3File_ShouldHaveCorrectMimeType()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("type=\"audio/mpeg\"");
    }

    [Fact]
    public void BuildRssFeed_M4aFile_ShouldHaveCorrectMimeType()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, filePathAudio: "test/audio.m4a", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("type=\"audio/mp4\"");
    }

    [Fact]
    public void BuildRssFeed_Mp4File_ShouldHaveCorrectMimeType()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, filePathVideo: "test/video.mp4", fileSizeVideo: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Video, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("type=\"video/mp4\"");
    }

    [Fact]
    public void BuildRssFeed_EpisodeWithoutAudioFile_ShouldBeSkippedForAudioFeed()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, title: "Has Audio", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024),
            CreateTestEpisode(channel, title: "No Audio", filePathAudio: null, fileSizeAudio: null)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<title>Has Audio</title>");
        result.Should().NotContain("<title>No Audio</title>");
    }

    [Fact]
    public void BuildRssFeed_EpisodeWithoutVideoFile_ShouldBeSkippedForVideoFeed()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, title: "Has Video", filePathVideo: "test/video.mp4", fileSizeVideo: 1024),
            CreateTestEpisode(channel, title: "No Video", filePathVideo: null, fileSizeVideo: null)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Video, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<title>Has Video</title>");
        result.Should().NotContain("<title>No Video</title>");
    }

    #endregion

    #region Duration Formatting Tests

    [Fact]
    public void BuildRssFeed_EpisodeWithDuration_ShouldIncludeITunesDurationInHHMMSS()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, durationSeconds: 3661, filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<itunes:duration>1:01:01</itunes:duration>");
    }

    [Fact]
    public void BuildRssFeed_EpisodeWithDurationUnderHour_ShouldUseMMSSFormat()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, durationSeconds: 90, filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<itunes:duration>1:30</itunes:duration>");
    }

    [Fact]
    public void BuildRssFeed_EpisodeWithDurationLessThanMinute_ShouldUseMMSSFormat()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, durationSeconds: 45, filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<itunes:duration>0:45</itunes:duration>");
    }

    [Fact]
    public void BuildRssFeed_EpisodeWithoutDuration_ShouldNotIncludeITunesDuration()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, durationSeconds: null, filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        // There's no duration element because durationSeconds is null - but there might be iTunes elements
        // We just check that no duration element is present
        var doc = XDocument.Parse(result);
        var items = doc.Root?.Element("channel")?.Elements("item").ToList();
        items.Should().HaveCount(1);
    }

    [Fact]
    public void BuildRssFeed_LongDuration_ShouldFormatCorrectly()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, durationSeconds: 5445, filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<itunes:duration>1:30:45</itunes:duration>");
    }

    #endregion

    #region iTunes Item Elements Tests

    [Fact]
    public void BuildRssFeed_WithEpisode_ShouldIncludeITunesTitle()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, title: "Episode 42", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<itunes:title>Episode 42</itunes:title>");
    }

    [Fact]
    public void BuildRssFeed_WithEpisode_ShouldIncludeITunesExplicitFalse()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        // Should have item-level explicit set to false
        var doc = XDocument.Parse(result);
        var item = doc.Root?.Element("channel")?.Elements("item").First();
        var itunesExplicit = item?.Element(XName.Get("explicit", "http://www.itunes.com/dtds/podcast-1.0.dtd"));
        itunesExplicit.Should().NotBeNull();
        itunesExplicit!.Value.Should().Be("false");
    }

    [Fact]
    public void BuildRssFeed_WithEpisode_ShouldIncludeITunesEpisodeTypeFull()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<itunes:episodeType>full</itunes:episodeType>");
    }

    [Fact]
    public void BuildRssFeed_WithEpisodeAndThumbnail_ShouldIncludeITunesImage()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, thumbnailUrl: "http://localhost:8080/feeds/test/thumb.jpg", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<itunes:image href=\"http://localhost:8080/feeds/test/thumb.jpg\" />");
    }

    [Fact]
    public void BuildRssFeed_WithEpisode_ShouldIncludeContentEncoded()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, description: "Full episode description", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        result.Should().Contain("<content:encoded><![CDATA[Full episode description]]></content:encoded>");
    }

    #endregion

    #region Episode Ordering Tests

    [Fact]
    public void BuildRssFeed_ShouldOrderEpisodesByPublishedAtDescending()
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
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        var doc = XDocument.Parse(result);
        var itemTitles = doc.Root?.Element("channel")?.Elements("item").Select(i => i.Element("title")?.Value).ToList();
        itemTitles.Should().HaveCount(3);
        itemTitles![0].Should().Be("New Episode");
        itemTitles[1].Should().Be("Middle Episode");
        itemTitles[2].Should().Be("Old Episode");
    }

    [Fact]
    public void BuildRssFeed_EpisodeWithoutPublishedDate_ShouldAppearAtEnd()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, title: "Has Date", publishedAt: new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero), filePathAudio: "test/audio.mp3", fileSizeAudio: 1024),
            CreateTestEpisode(channel, title: "No Date", publishedAt: null, filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        var doc = XDocument.Parse(result);
        var itemTitles = doc.Root?.Element("channel")?.Elements("item").Select(i => i.Element("title")?.Value).ToList();
        itemTitles.Should().HaveCount(2);
        itemTitles![0].Should().Be("Has Date");
        itemTitles[1].Should().Be("No Date");
    }

    #endregion

    #region XML Escaping Tests

    [Fact]
    public void BuildRssFeed_TitleWithSpecialCharacters_ShouldEscapeCorrectly()
    {
        // Arrange
        var channel = CreateTestChannel(title: "Tech & Talk <Podcast> \"Weekly\"");
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        // Note: Double quotes in text content don't need to be escaped in XML
        result.Should().Contain("Tech &amp; Talk &lt;Podcast&gt; \"Weekly\"");
    }

    [Fact]
    public void BuildRssFeed_DescriptionWithXmlCharacters_ShouldEscapeCorrectly()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, description: "Description with <script>alert('xss')</script> & more", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        // The description should be escaped in the regular description element
        result.Should().Contain("&lt;script&gt;alert('xss')&lt;/script&gt; &amp; more");
    }

    [Fact]
    public void BuildRssFeed_DescriptionInContentEncoded_ShouldBeWrappedInCData()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>
        {
            CreateTestEpisode(channel, description: "Description with <html> tags & entities", filePathAudio: "test/audio.mp3", fileSizeAudio: 1024)
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        // content:encoded should wrap in CDATA
        result.Should().Contain("<content:encoded><![CDATA[Description with <html> tags & entities]]></content:encoded>");
    }

    #endregion

    #region Empty Feed Tests

    [Fact]
    public void BuildRssFeed_WithNoEpisodes_ShouldReturnValidEmptyFeed()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = new List<Episode>();

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert
        var doc = XDocument.Parse(result);
        var items = doc.Root?.Element("channel")?.Elements("item").ToList();
        items.Should().BeEmpty();
    }

    #endregion

    #region Complete Feed Example Test

    [Fact]
    public void BuildRssFeed_WithCompleteData_ShouldGenerateValidRss2_0Feed()
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
                filePathAudio: "tech-talk/audio/abc123.mp3",
                fileSizeAudio: 52428800,
                thumbnailUrl: "http://localhost:8080/feeds/tech-talk/episodes/abc123-thumb.jpg"
            )
        };

        // Act
        var result = _builder.BuildRssFeed(channel, episodes, FeedType.Audio, BaseUrl, FeedPath);

        // Assert - Verify it parses as valid XML
        var doc = XDocument.Parse(result);
        doc.Should().NotBeNull();

        // Verify RSS version
        doc.Root?.Attribute("version")?.Value.Should().Be("2.0");

        // Verify namespaces
        var nsAttribute = doc.Root?.Attribute(XName.Get("itunes", "http://www.w3.org/2000/xmlns/"));
        nsAttribute?.Value.Should().Be("http://www.itunes.com/dtds/podcast-1.0.dtd");

        // Verify channel elements
        var channelElement = doc.Root?.Element("channel");
        channelElement.Should().NotBeNull();
        channelElement?.Element("title")?.Value.Should().Be("Tech Talk Weekly");
        channelElement?.Element("link")?.Value.Should().Be("https://youtube.com/@techtalk");
        channelElement?.Element("description")?.Value.Should().Be("Technology news and discussion from industry experts.");

        // Verify item elements
        var item = channelElement?.Element("item");
        item.Should().NotBeNull();
        item?.Element("title")?.Value.Should().Be("Episode 42: The AI Revolution");
        item?.Element("link")?.Value.Should().Be("https://www.youtube.com/watch?v=abc123");
        item?.Element("description")?.Value.Should().Be("An in-depth discussion about artificial intelligence trends.");
        item?.Element("guid")?.Value.Should().Be("yt:abc123");
        item?.Element("guid")?.Attribute("isPermaLink")?.Value.Should().Be("false");

        // Verify enclosure
        var enclosure = item?.Element("enclosure");
        enclosure.Should().NotBeNull();
        enclosure?.Attribute("url")?.Value.Should().Be("http://localhost:8080/feeds/tech-talk/audio/abc123.mp3");
        enclosure?.Attribute("length")?.Value.Should().Be("52428800");
        enclosure?.Attribute("type")?.Value.Should().Be("audio/mpeg");
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
            FilePathAudio = filePathAudio,
            FileSizeAudio = fileSizeAudio,
            FilePathVideo = filePathVideo,
            FileSizeVideo = fileSizeVideo,
            Status = EpisodeStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    #endregion
}