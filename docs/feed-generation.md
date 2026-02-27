# Feed Generation Design

This document describes the RSS and Atom feed generation system for Yallarhorn, including URL routing, feed specifications, dynamic generation, and validation strategies.

## Table of Contents

- [Overview](#overview)
- [Feed URL Structure](#feed-url-structure)
- [RSS 2.0 Specification](#rss-20-specification)
- [Atom 1.0 Specification](#atom-10-specification)
- [Dynamic Generation from SQLite](#dynamic-generation-from-sqlite)
- [Episode Selection Logic](#episode-selection-logic)
- [Validation Strategy](#validation-strategy)
- [Caching and Performance](#caching-and-performance)

---

## Overview

Yallarhorn generates RSS and Atom feeds that enable podcast clients to subscribe to YouTube channels as audio or video podcasts. The system supports:

- **Per-channel feeds**: Individual feeds for each monitored channel
- **Audio feeds**: MP3/M4A audio enclosures for podcast apps
- **Video feeds**: MP4 video enclosures for video podcast clients
- **Combined feeds**: Aggregated feed of all channels
- **Podcast extensions**: iTunes podcast namespace for rich metadata

### Feed Types by Channel Configuration

| Channel `feed_type` | Feeds Generated |
|---------------------|-----------------|
| `audio` | Audio RSS feed only |
| `video` | Video RSS feed only |
| `both` | Both audio and video RSS feeds |

---

## Feed URL Structure

### Per-Channel Feeds

Each channel generates feeds based on its `feed_type` configuration:

```
/feed/{channel-id}/audio.rss     # Audio RSS feed
/feed/{channel-id}/video.rss     # Video RSS feed
/feed/{channel-id}/atom.xml      # Atom feed (primary format based on feed_type)
```

### Global Combined Feed

A single aggregated feed containing episodes from all enabled channels:

```
/feeds/all.rss                   # Combined audio RSS feed
/feeds/all-video.rss             # Combined video RSS feed
/feeds/all.atom                  # Combined Atom feed
```

### Media File URLs

Individual episode media files are served at:

```
/feeds/{channel-slug}/audio/{video-id}.{ext}    # Audio file
/feeds/{channel-slug}/video/{video-id}.{ext}    # Video file
```

### URL Generation Logic

The `server.base_url` configuration setting defines the public URL used in feed generation:

```yaml
server:
  base_url: "http://localhost:8080"
  feed_path: "/feeds"
```

Feed URLs are constructed as:

```
{base_url}/feed/{channel-id}/audio.rss
{base_url}/feed/{channel-id}/video.rss
{base_url}/feeds/all.rss
```

Media enclosure URLs:

```
{base_url}/{feed_path}/{channel-slug}/audio/{video-id}.mp3
{base_url}/{feed_path}/{channel-slug}/video/{video-id}.mp4
```

### Channel ID vs Slug

The feed URL uses the channel's database `id` (UUID or slug-based) for uniqueness. A slugified version of the channel title is used for file paths:

| Use Case | Source | Example |
|----------|--------|---------|
| Feed URL path | `channels.id` | `ch-abc123` |
| Media file path | Derived from `channels.title` | `tech-talk-weekly` |

Slugification rules:
1. Lowercase all characters
2. Replace non-alphanumeric characters with hyphens
3. Collapse consecutive hyphens to single hyphen
4. Remove leading/trailing hyphens
5. Limit to 50 characters

---

## RSS 2.0 Specification

Yallarhorn generates RSS 2.0 feeds optimized for podcast client compatibility.

### Required Channel Elements

Per [RSS 2.0 Specification](https://www.rssboard.org/rss-specification):

| Element | Source | Example |
|---------|--------|---------|
| `<title>` | `channels.title` | "Tech Talk Weekly" |
| `<link>` | `channels.url` | "https://youtube.com/@techtalk" |
| `<description>` | `channels.description` | "Technology news and discussion" |

### Required Item Elements

Each episode item includes:

| Element | Source | Example |
|---------|--------|---------|
| `<title>` | `episodes.title` | "Episode 42: AI Revolution" |
| `<link>` | YouTube watch URL | "https://youtube.com/watch?v=abc123" |
| `<description>` | `episodes.description` | "Discussion about AI trends..." |

### Enclosure Tag

The `<enclosure>` tag is critical for podcast clients:

```xml
<enclosure 
  url="http://localhost:8080/feeds/tech-talk/audio/abc123.mp3"
  length="52428800"
  type="audio/mpeg"
/>
```

#### Enclosure Attributes

| Attribute | Source | Audio Example | Video Example |
|-----------|--------|---------------|---------------|
| `url` | Constructed from `base_url`, `file_path_audio/video` | `http://.../*.mp3` | `http://.../*.mp4` |
| `length` | `episodes.file_size_audio/video` | `52428800` | `524288000` |
| `type` | Derived from file extension | `audio/mpeg` | `video/mp4` |

#### MIME Types by Format

| File Extension | MIME Type |
|----------------|-----------|
| `.mp3` | `audio/mpeg` |
| `.m4a` | `audio/mp4` |
| `.aac` | `audio/aac` |
| `.ogg` | `audio/ogg` |
| `.mp4` | `video/mp4` |
| `.m4v` | `video/mp4` |
| `.webm` | `video/webm` |

### Optional Elements

#### Channel-Level

```xml
<language>en-us</language>
<copyright>All content © Channel Owner</copyright>
<managingEditor>editor@example.com</managingEditor>
<webMaster>admin@example.com</webMaster>
<pubDate>Mon, 15 Jan 2024 10:30:00 GMT</pubDate>
<lastBuildDate>Mon, 15 Jan 2024 12:00:00 GMT</lastBuildDate>
<category>Technology</category>
<ttl>60</ttl>
<image>
  <url>http://localhost:8080/feeds/tech-talk/cover.jpg</url>
  <title>Tech Talk Weekly</title>
  <link>https://youtube.com/@techtalk</link>
</image>
```

#### Item-Level

```xml
<author>channel-name</author>
<category>Technology</category>
<comments>https://youtube.com/watch?v=abc123&lc=comments</comments>
<guid isPermaLink="false">yt:abc123</guid>
<pubDate>Mon, 15 Jan 2024 09:00:00 GMT</pubDate>
<source url="http://localhost:8080/feed/ch-abc123/audio.rss">Tech Talk Weekly</source>
```

### iTunes Podcast Extensions

Yallarhorn includes iTunes podcast namespace extensions for enhanced podcast client compatibility.

#### Namespace Declaration

```xml
<rss version="2.0" 
     xmlns:itunes="http://www.itunes.com/dtds/podcast-1.0.dtd"
     xmlns:content="http://purl.org/rss/1.0/modules/content/">
```

#### Channel-Level iTunes Elements

```xml
<itunes:type>episodic</itunes:type>
<itunes:author>Tech Talk Weekly</itunes:author>
<itunes:owner>
  <itunes:name>Tech Talk Weekly</itunes:name>
  <itunes:email>contact@techtalk.com</itunes:email>
</itunes:owner>
<itunes:image href="http://localhost:8080/feeds/tech-talk/cover.jpg"/>
<itunes:category text="Technology"/>
<itunes:explicit>false</itunes:explicit>
<itunes:summary>Technology news and discussion</itunes:summary>
```

#### Item-Level iTunes Elements

```xml
<itunes:title>Episode 42: AI Revolution</itunes:title>
<itunes:duration>1:30:45</itunes:duration>
<itunes:explicit>false</itunes:explicit>
<itunes:episodeType>full</itunes:episodeType>
<itunes:episode>42</itunes:episode>
<itunes:season>1</itunes:season>
<itunes:image href="http://localhost:8080/feeds/tech-talk/episodes/abc123-thumb.jpg"/>
<itunes:summary>Discussion about AI trends...</itunes:summary>
<content:encoded><![CDATA[<p>Full HTML description...</p>]]></content:encoded>
```

#### iTunes Element Mapping

| Element | Source | Notes |
|---------|--------|-------|
| `itunes:author` | `channels.title` | Channel name |
| `itunes:owner/itunes:name` | `channels.title` | Channel name |
| `itunes:image` | `channels.thumbnail_url` | 1400x1400 minimum recommended |
| `itunes:summary` | `channels.description` | Plain text, max 4000 chars |
| `itunes:category` | Derived from `channels.tags` | iTunes category hierarchy |
| `itunes:explicit` | Default `false` | Can be configured per-channel |
| `itunes:type` | Default `episodic` | `episodic` or `serial` |
| `itunes:duration` | `episodes.duration_seconds` | Formatted as HH:MM:SS or MM:SS |
| `itunes:episodeType` | Default `full` | `full`, `trailer`, or `bonus` |

#### Duration Formatting

Convert `duration_seconds` to iTunes duration format:

```csharp
public static string FormatDuration(int seconds)
{
    var ts = TimeSpan.FromSeconds(seconds);
    if (ts.TotalHours >= 1)
        return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    return $"{ts.Minutes}:{ts.Seconds:D2}";
}

// Examples:
// 90 seconds → "1:30"
// 3661 seconds → "1:01:01"
```

### Complete RSS 2.0 Example

```xml
<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" 
     xmlns:itunes="http://www.itunes.com/dtds/podcast-1.0.dtd"
     xmlns:content="http://purl.org/rss/1.0/modules/content/">
  <channel>
    <title>Tech Talk Weekly</title>
    <link>https://www.youtube.com/@techtalk</link>
    <description>Technology news and discussion from industry experts.</description>
    <language>en-us</language>
    <lastBuildDate>Mon, 15 Jan 2024 12:00:00 GMT</lastBuildDate>
    <pubDate>Mon, 15 Jan 2024 10:30:00 GMT</pubDate>
    <ttl>60</ttl>
    <image>
      <url>http://localhost:8080/feeds/tech-talk/cover.jpg</url>
      <title>Tech Talk Weekly</title>
      <link>https://www.youtube.com/@techtalk</link>
    </image>
    
    <!-- iTunes Extensions -->
    <itunes:type>episodic</itunes:type>
    <itunes:author>Tech Talk Weekly</itunes:author>
    <itunes:owner>
      <itunes:name>Tech Talk Weekly</itunes:name>
      <itunes:email>contact@techtalk.com</itunes:email>
    </itunes:owner>
    <itunes:image href="http://localhost:8080/feeds/tech-talk/cover.jpg"/>
    <itunes:category text="Technology"/>
    <itunes:explicit>false</itunes:explicit>
    <itunes:summary>Technology news and discussion from industry experts.</itunes:summary>
    
    <!-- Episode Item -->
    <item>
      <title>Episode 42: The AI Revolution</title>
      <link>https://www.youtube.com/watch?v=abc123</link>
      <description>An in-depth discussion about artificial intelligence trends, 
                   ChatGPT, and the future of generative AI in software development.</description>
      <guid isPermaLink="false">yt:abc123</guid>
      <pubDate>Mon, 15 Jan 2024 09:00:00 GMT</pubDate>
      <enclosure 
        url="http://localhost:8080/feeds/tech-talk/audio/abc123.mp3"
        length="52428800"
        type="audio/mpeg"
      />
      <itunes:title>Episode 42: The AI Revolution</itunes:title>
      <itunes:duration>1:30:45</itunes:duration>
      <itunes:explicit>false</itunes:explicit>
      <itunes:episodeType>full</itunes:episodeType>
      <itunes:episode>42</itunes:episode>
      <itunes:image href="http://localhost:8080/feeds/tech-talk/episodes/abc123-thumb.jpg"/>
      <content:encoded><![CDATA[
        <p>An in-depth discussion about artificial intelligence trends.</p>
        <h2>Topics Covered</h2>
        <ul>
          <li>Large Language Models</li>
          <li>ChatGPT and alternatives</li>
          <li>AI in software development</li>
        </ul>
      ]]></content:encoded>
    </item>
  </channel>
</rss>
```

---

## Atom 1.0 Specification

Atom 1.0 is provided as an alternative feed format for clients that prefer it.

### Namespace and Structure

```xml
<?xml version="1.0" encoding="UTF-8"?>
<feed xmlns="http://www.w3.org/2005/Atom">
  <id>http://localhost:8080/feed/ch-abc123/atom.xml</id>
  <title>Tech Talk Weekly</title>
  <subtitle>Technology news and discussion</subtitle>
  <link href="https://www.youtube.com/@techtalk"/>
  <link rel="self" href="http://localhost:8080/feed/ch-abc123/atom.xml"/>
  <link rel="alternate" href="http://localhost:8080/feed/ch-abc123/audio.rss" 
        type="application/rss+xml"/>
  <updated>2024-01-15T12:00:00Z</updated>
  <author>
    <name>Tech Talk Weekly</name>
  </author>
  
  <entry>
    <id>yt:abc123</id>
    <title>Episode 42: The AI Revolution</title>
    <link href="https://www.youtube.com/watch?v=abc123"/>
    <link rel="enclosure" 
          href="http://localhost:8080/feeds/tech-talk/audio/abc123.mp3"
          length="52428800"
          type="audio/mpeg"/>
    <summary>An in-depth discussion about artificial intelligence trends...</summary>
    <content type="html"><![CDATA[<p>Full description...</p>]]></content>
    <published>2024-01-15T09:00:00Z</published>
    <updated>2024-01-15T10:30:00Z</updated>
  </entry>
</feed>
```

### Element Mapping

| Atom Element | Source | Notes |
|--------------|--------|-------|
| `<id>` | Feed URL or `yt:` prefix + video_id | Unique identifier |
| `<title>` | `channels.title` / `episodes.title` | Human-readable title |
| `<subtitle>` | `channels.description` | Feed description |
| `<link>` (self) | Feed URL | `rel="self"` |
| `<link>` (alternate) | Original source URL | `rel="alternate"` |
| `<link>` (enclosure) | Media file URL | `rel="enclosure"` |
| `<updated>` | ISO 8601 timestamp | Feed/entry last modified |
| `<published>` | `episodes.published_at` | Original publish date |
| `<author>/<name>` | `channels.title` | Channel name |
| `<summary>` | `episodes.description` | Plain text summary |
| `<content>` | `episodes.description` | HTML content (if available) |

### Enclosure in Atom

Atom uses `link` elements with `rel="enclosure"` attribute:

```xml
<link rel="enclosure" 
      href="http://localhost:8080/feeds/tech-talk/audio/abc123.mp3"
      length="52428800"
      type="audio/mpeg"
      title="Audio Download"/>
```

---

## Dynamic Generation from SQLite

Feeds are generated dynamically from the SQLite database rather than pre-generated static files.

### Query: Per-Channel Feed Episodes

```sql
-- Get episodes for a channel's feed (rolling window)
SELECT 
    e.id,
    e.video_id,
    e.title,
    e.description,
    e.thumbnail_url,
    e.duration_seconds,
    e.published_at,
    e.downloaded_at,
    e.file_path_audio,
    e.file_path_video,
    e.file_size_audio,
    e.file_size_video,
    c.id as channel_id,
    c.title as channel_title,
    c.url as channel_url,
    c.description as channel_description,
    c.thumbnail_url as channel_thumbnail
FROM episodes e
JOIN channels c ON e.channel_id = c.id
WHERE e.channel_id = ? 
  AND e.status = 'completed'
  AND e.downloaded_at IS NOT NULL
  AND (
      -- Include audio if feed needs it
      (? = 'audio' AND e.file_path_audio IS NOT NULL)
      OR
      -- Include video if feed needs it  
      (? = 'video' AND e.file_path_video IS NOT NULL)
      OR
      -- Include if 'both' and has either file
      (? = 'both' AND (e.file_path_audio IS NOT NULL OR e.file_path_video IS NOT NULL))
  )
ORDER BY e.published_at DESC
LIMIT ?;  -- episode_count_config from channel
```

### Query: Combined Feed Episodes

```sql
-- Get episodes for combined global feed
SELECT 
    e.id,
    e.video_id,
    e.title,
    e.description,
    e.thumbnail_url,
    e.duration_seconds,
    e.published_at,
    e.file_path_audio,
    e.file_path_video,
    e.file_size_audio,
    e.file_size_video,
    c.id as channel_id,
    c.title as channel_title,
    c.url as channel_url
FROM episodes e
JOIN channels c ON e.channel_id = c.id
WHERE c.enabled = 1
  AND e.status = 'completed'
  AND e.downloaded_at IS NOT NULL
  AND e.file_path_audio IS NOT NULL  -- or video for video feed
ORDER BY e.published_at DESC
LIMIT 100;  -- Global feed limit
```

### Query: Feed Metadata

```sql
-- Get channel metadata for feed header
SELECT 
    id,
    title,
    description,
    url,
    thumbnail_url,
    episode_count_config,
    feed_type,
    updated_at
FROM channels
WHERE id = ?;
```

### Rolling Window Implementation

The rolling window determines how many episodes appear in each feed:

1. **Per-channel limit**: Set via `episode_count_config` (default: 50)
2. **Query execution**: Use `LIMIT` clause with channel's config value
3. **Episode expiration**: Episodes outside the window are marked for deletion

```csharp
public async Task<List<Episode>> GetFeedEpisodesAsync(string channelId, int limit, string feedType)
{
    var sql = @"
        SELECT * FROM episodes
        WHERE channel_id = @channelId
          AND status = 'completed'
          AND downloaded_at IS NOT NULL
          AND HasRequiredFile(@feedType)
        ORDER BY published_at DESC
        LIMIT @limit";
    
    // feedType determines which file to require:
    // - "audio": file_path_audio IS NOT NULL
    // - "video": file_path_video IS NOT NULL
    // - "both": Either file available
}
```

### Response Headers

Dynamic feeds should include appropriate HTTP headers:

```
HTTP/1.1 200 OK
Content-Type: application/rss+xml; charset=utf-8
Cache-Control: public, max-age=300, stale-while-revalidate=60
Last-Modified: Mon, 15 Jan 2024 12:00:00 GMT
ETag: "abc123-def456"
```

### Conditional Requests

Support `If-Modified-Since` and `If-None-Match` headers:

```csharp
public async Task<FeedResult> GenerateFeedAsync(string channelId, string feedType, 
    DateTime? ifModifiedSince, string ifNoneMatch)
{
    var channel = await GetChannelAsync(channelId);
    
    // Check if feed has changed
    if (ifModifiedSince.HasValue && channel.updated_at <= ifModifiedSince.Value)
        return FeedResult.NotModified();
    
    var etag = GenerateETag(channel.updated_at);
    if (ifNoneMatch == etag)
        return FeedResult.NotModified();
    
    // Generate feed content
    var episodes = await GetFeedEpisodesAsync(channelId, channel.episode_count_config, feedType);
    var feedXml = BuildRssFeed(channel, episodes, feedType);
    
    return FeedResult.Ok(feedXml, etag, channel.updated_at);
}
```

---

## Episode Selection Logic

### Selection Criteria

Episodes are included in feeds based on multiple criteria:

#### Status Requirements

| Criterion | Required Value |
|-----------|----------------|
| `status` | `completed` |
| `downloaded_at` | Not NULL (download completed) |
| `file_path_audio` or `file_path_video` | Not NULL (files exist) |

#### File Availability by Feed Type

| Feed Type | Required Files |
|-----------|---------------|
| Audio feed | `file_path_audio IS NOT NULL` AND `file_size_audio IS NOT NULL` |
| Video feed | `file_path_video IS NOT NULL` AND `file_size_video IS NOT NULL` |

#### Ordering

Episodes are ordered by `published_at DESC` (newest first) to ensure:
- Latest episodes appear at the top
- Consistent ordering for podcast clients
- Stable feed when episodes are added

### Episode Limiting

The `episode_count_config` controls the rolling window:

```csharp
public int CalculateEpisodeLimit(Channel channel)
{
    // Validate config
    if (channel.episode_count_config < 1)
        return 10;  // Minimum safe value
    if (channel.episode_count_config > 1000)
        return 1000;  // Maximum allowed
    
    return channel.episode_count_config;
}
```

### Combined Feed Aggregation

The global combined feed aggregates from all enabled channels:

```sql
SELECT e.*, c.title as channel_title
FROM episodes e
JOIN channels c ON e.channel_id = c.id
WHERE c.enabled = 1
  AND e.status = 'completed'
  AND e.downloaded_at IS NOT NULL
  AND e.file_path_audio IS NOT NULL
ORDER BY e.published_at DESC
LIMIT 100;
```

#### Combined Feed Episode Cap

- Maximum 100 episodes in combined feed
- Ordered chronologically (newest first)
- Includes channel attribution in each item

```xml
<item>
  <title>[Tech Talk] Episode 42: AI Revolution</title>
  <!-- Channel prefix in title for clarity -->
  <source url="...">Tech Talk Weekly</source>
  <itunes:author>Tech Talk Weekly</itunes:author>
</item>
```

---

## Validation Strategy

### Feed Validation

Generated feeds should pass podcast feed validators:

#### Validation Tools

1. **Podcast Validator**: https://podba.se/validate/
2. **W3C RSS Validator**: https://validator.w3.org/feed/
3. **iTunes Validator**: Built into Apple Podcasts Connect

#### Validation Checks

| Check | Description | Severity |
|-------|-------------|----------|
| Required channel elements | `<title>`, `<link>`, `<description>` | Error |
| Required item elements | `<title>`, `<link>`, `<description>` | Error |
| Valid enclosure | `url`, `length` (numeric), `type` (valid MIME) | Error |
| iTunes image | 1400x1400 minimum, JPG/PNG | Warning |
| iTunes duration | Valid time format | Warning |
| iTunes category | Valid iTunes category name | Warning |
| XML well-formedness | Valid XML structure | Error |
| Encoding | UTF-8 encoding specified | Warning |

### Runtime Validation

#### XML Generation

Use an XML library to ensure well-formed output:

```csharp
public string GenerateRssFeed(Channel channel, List<Episode> episodes)
{
    var settings = new XmlWriterSettings
    {
        Encoding = Encoding.UTF8,
        Indent = true,
        OmitXmlDeclaration = false
    };
    
    using var sw = new StringWriter();
    using var writer = XmlWriter.Create(sw, settings);
    
    writer.WriteStartDocument();
    writer.WriteStartElement("rss");
    writer.WriteAttributeString("version", "2.0");
    writer.WriteAttributeString("xmlns", "itunes", null, 
        "http://www.itunes.com/dtds/podcast-1.0.dtd");
    writer.WriteAttributeString("xmlns", "content", null,
        "http://purl.org/rss/1.0/modules/content/");
    
    writer.WriteStartElement("channel");
    // ... build feed elements
    
    writer.WriteEndElement(); // channel
    writer.WriteEndElement(); // rss
    writer.WriteEndDocument();
    
    return sw.ToString();
}
```

#### URL Validation

Validate all URLs in the feed:

```csharp
public void ValidateFeedUrls(Feed feed)
{
    // Channel URL
    if (!Uri.TryCreate(feed.Link, UriKind.Absolute, out _))
        throw new InvalidFeedException($"Invalid channel link: {feed.Link}");
    
    foreach (var item in feed.Items)
    {
        // Episode link
        if (!Uri.TryCreate(item.Link, UriKind.Absolute, out _))
            logger.LogWarning("Invalid episode link: {Link}", item.Link);
        
        // Enclosure URL
        if (!Uri.TryCreate(item.Enclosure.Url, UriKind.Absolute, out _))
            throw new InvalidFeedException($"Invalid enclosure URL: {item.Enclosure.Url}");
    }
}
```

#### File Size Validation

Ensure enclosure `length` matches actual file size:

```csharp
public void ValidateEnclosureLength(Episode episode, string audioPath)
{
    var fileInfo = new FileInfo(audioPath);
    if (fileInfo.Exists && fileInfo.Length != episode.file_size_audio)
    {
        logger.LogWarning(
            "Enclosure size mismatch for {VideoId}: expected {Expected}, actual {Actual}",
            episode.video_id, episode.file_size_audio, fileInfo.Length);
        
        // Update database with correct size
        episode.file_size_audio = (int)fileInfo.Length;
    }
}
```

### Integration Testing

Test feed generation with sample data:

```csharp
public class FeedGenerationTests
{
    [Fact]
    public void GenerateFeed_WithEpisodes_ProducesValidXml()
    {
        // Arrange
        var channel = CreateTestChannel();
        var episodes = CreateTestEpisodes(5);
        var generator = new FeedGenerator();
        
        // Act
        var feed = generator.GenerateRss(channel, episodes, "audio");
        
        // Assert
        var doc = XDocument.Parse(feed);
        Assert.NotNull(doc.Root);
        Assert.Equal("rss", doc.Root.Name.LocalName);
    }
    
    [Fact]
    public void GenerateFeed_WithITunes_IncludesCorrectNamespace()
    {
        // Arrange
        var generator = new FeedGenerator();
        
        // Act
        var feed = generator.GenerateRss(CreateTestChannel(), new List<Episode>(), "audio");
        
        // Assert
        Assert.Contains("xmlns:itunes", feed);
        Assert.Contains("http://www.itunes.com/dtds/podcast-1.0.dtd", feed);
    }
    
    [Fact]
    public void GenerateFeed_WithEpisodes_IncludesEnclosure()
    {
        // Arrange
        var episode = new Episode
        {
            video_id = "test123",
            title = "Test Episode",
            file_path_audio = "test/test123.mp3",
            file_size_audio = 52428800
        };
        var generator = new FeedGenerator();
        
        // Act
        var feed = generator.GenerateRss(CreateTestChannel(), 
            new List<Episode> { episode }, "audio");
        
        // Assert
        Assert.Contains("<enclosure", feed);
        Assert.Contains("url=\"http", feed);
        Assert.Contains("length=\"52428800\"", feed);
        Assert.Contains("type=\"audio/mpeg\"", feed);
    }
}
```

### Continuous Validation

Implement a periodic feed validation check:

```csharp
public class FeedValidationService : IHostedService
{
    public async Task ValidateAllFeedsAsync()
    {
        var channels = await channelRepository.GetAllEnabledAsync();
        
        foreach (var channel in channels)
        {
            var feedUrl = $"{baseUrl}/feed/{channel.id}/audio.rss";
            var result = await validator.ValidateAsync(feedUrl);
            
            if (!result.IsValid)
            {
                logger.LogError(
                    "Feed validation failed for channel {ChannelId}: {Errors}",
                    channel.id, string.Join(", ", result.Errors));
                
                // Alert administrators
                await alerting.NotifyAsync("Feed validation failed", result.Errors);
            }
        }
    }
}
```

---

## Caching and Performance

### HTTP Caching

Use standard HTTP caching headers to reduce server load:

```
Cache-Control: public, max-age=300, stale-while-revalidate=60
ETag: "v1-abc123"
Last-Modified: Mon, 15 Jan 2024 12:00:00 GMT
```

- `max-age=300`: Cache for 5 minutes
- `stale-while-revalidate=60`: Serve stale content for 1 minute while refreshing
- `ETag`: Unique identifier based on channel update timestamp

### In-Memory Caching

Cache generated feeds in memory for frequently accessed channels:

```csharp
public class FeedCache
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
    
    public async Task<string> GetOrCreateFeedAsync(
        string channelId, 
        string feedType,
        Func<Task<string>> generateFeed)
    {
        var cacheKey = $"feed:{channelId}:{feedType}";
        
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheDuration;
            entry.SlidingExpiration = TimeSpan.FromMinutes(2);
            
            return await generateFeed();
        });
    }
    
    public void Invalidate(string channelId)
    {
        // Invalidate all feed types for this channel
        _cache.Remove($"feed:{channelId}:audio");
        _cache.Remove($"feed:{channelId}:video");
    }
}
```

### Cache Invalidation

Invalidate cache when:
- Channel metadata changes (title, description, etc.)
- New episode download completes
- Episode is deleted
- `episode_count_config` changes

```csharp
public class FeedInvalidationHandler
{
    public void OnEpisodeCompleted(string channelId)
    {
        _cache.Invalidate(channelId);
        _combinedFeedCache.Invalidate();
    }
    
    public void OnChannelUpdated(string channelId)
    {
        _cache.Invalidate(channelId);
    }
}
```

---

## See Also

- [Data Model & Schema Design](./data-model.md) - Database schema documentation
- [Configuration Schema](./configuration.md) - YAML configuration reference
- [API Specification](./api-specification.md) - REST API documentation

## References

- [RSS 2.0 Specification](https://www.rssboard.org/rss-specification)
- [Atom Syndication Format (RFC 4287)](https://tools.ietf.org/html/rfc4287)
- [iTunes Podcast RSS Tags](https://podcasters.apple.com/support/823-podcast-requirements)
- [Podcast Namespace Specification](https://github.com/Podcastindex-org/podcast-namespace)
- [W3C Feed Validation Service](https://validator.w3.org/feed/)