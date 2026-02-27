namespace Yallarhorn.Controllers;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Yallarhorn.Data.Enums;
using Yallarhorn.Models;
using Yallarhorn.Services;

/// <summary>
/// Controller for serving RSS and Atom podcast feeds.
/// </summary>
[ApiController]
[Route("feed")]
public class FeedController : ControllerBase
{
    private readonly IFeedService _feedService;
    private readonly ICombinedFeedService _combinedFeedService;
    private readonly IFeedCache _feedCache;
    private readonly ILogger<FeedController> _logger;

    /// <summary>
    /// Valid audio MIME types.
    /// </summary>
    private static readonly HashSet<string> AudioMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/mpeg",      // MP3
        "audio/mp4",       // M4A
        "audio/aac",        // AAC
        "audio/ogg",        // OGG
        "audio/wav",        // WAV
        "audio/x-m4a"      // M4A (alternative)
    };

    /// <summary>
    /// Valid video MIME types.
    /// </summary>
    private static readonly HashSet<string> VideoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4",       // MP4
        "video/webm",      // WebM
        "video/x-m4v"      // M4V (alternative)
    };

    /// <summary>
    /// Valid file extensions for each media type.
    /// </summary>
    private static readonly Dictionary<string, string[]> ValidExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio"] = new[] { ".mp3", ".m4a", ".aac", ".ogg", ".wav" },
        ["video"] = new[] { ".mp4", ".webm", ".m4v" }
    };

    /// <summary>
    /// MIME types by file extension.
    /// </summary>
    private static readonly Dictionary<string, string> ExtensionMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp3"] = "audio/mpeg",
        [".m4a"] = "audio/mp4",
        [".aac"] = "audio/aac",
        [".ogg"] = "audio/ogg",
        [".wav"] = "audio/wav",
        [".mp4"] = "video/mp4",
        [".webm"] = "video/webm",
        [".m4v"] = "video/mp4"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="FeedController"/> class.
    /// </summary>
    /// <param name="feedService">The feed service for per-channel feeds.</param>
    /// <param name="combinedFeedService">The combined feed service for aggregated feeds.</param>
    /// <param name="feedCache">The feed cache.</param>
    /// <param name="logger">The logger.</param>
    public FeedController(
        IFeedService feedService,
        ICombinedFeedService combinedFeedService,
        IFeedCache feedCache,
        ILogger<FeedController> logger)
    {
        _feedService = feedService;
        _combinedFeedService = combinedFeedService;
        _feedCache = feedCache;
        _logger = logger;
    }

    /// <summary>
    /// Gets the RSS 2.0 audio feed for a specific channel.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>RSS 2.0 audio feed.</returns>
    /// <remarks>
    /// Supported conditional request headers:
    /// - If-None-Match: Returns 304 Not Modified if ETag matches
    /// - If-Modified-Since: Returns 304 Not Modified if not modified since
    /// </remarks>
    [HttpGet("{channelId}/audio.rss")]
    [Produces("application/rss+xml")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAudioRss(string channelId, CancellationToken cancellationToken)
    {
        return await GetChannelFeedAsync(channelId, FeedType.Audio, "audio", cancellationToken);
    }

    /// <summary>
    /// Gets the RSS 2.0 video feed for a specific channel.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>RSS 2.0 video feed.</returns>
    /// <remarks>
    /// Supported conditional request headers:
    /// - If-None-Match: Returns 304 Not Modified if ETag matches
    /// - If-Modified-Since: Returns 304 Not Modified if not modified since
    /// </remarks>
    [HttpGet("{channelId}/video.rss")]
    [Produces("application/rss+xml")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVideoRss(string channelId, CancellationToken cancellationToken)
    {
        return await GetChannelFeedAsync(channelId, FeedType.Video, "video", cancellationToken);
    }

    /// <summary>
    /// Gets the Atom 1.0 feed for a specific channel.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Atom 1.0 feed.</returns>
    /// <remarks>
    /// Supported conditional request headers:
    /// - If-None-Match: Returns 304 Not Modified if ETag matches
    /// - If-Modified-Since: Returns 304 Not Modified if not modified since
    /// </remarks>
    [HttpGet("{channelId}/atom.xml")]
    [Produces("application/atom+xml")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAtom(string channelId, CancellationToken cancellationToken)
    {
        return await GetChannelFeedAsync(channelId, FeedType.Audio, "atom", cancellationToken);
    }

    /// <summary>
    /// Gets the combined RSS 2.0 audio feed from all channels.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Combined RSS 2.0 audio feed.</returns>
    /// <remarks>
    /// Supported conditional request headers:
    /// - If-None-Match: Returns 304 Not Modified if ETag matches
    /// - If-Modified-Since: Returns 304 Not Modified if not modified since
    /// </remarks>
    [HttpGet("/feeds/all.rss")]
    [Produces("application/rss+xml")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> GetAllAudioRss(CancellationToken cancellationToken)
    {
        return await GetCombinedFeedAsync(FeedType.Audio, "audio", cancellationToken);
    }

    /// <summary>
    /// Gets the combined RSS 2.0 video feed from all channels.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Combined RSS 2.0 video feed.</returns>
    /// <remarks>
    /// Supported conditional request headers:
    /// - If-None-Match: Returns 304 Not Modified if ETag matches
    /// - If-Modified-Since: Returns 304 Not Modified if not modified since
    /// </remarks>
    [HttpGet("/feeds/all-video.rss")]
    [Produces("application/rss+xml")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> GetAllVideoRss(CancellationToken cancellationToken)
    {
        return await GetCombinedFeedAsync(FeedType.Video, "video", cancellationToken);
    }

    /// <summary>
    /// Gets the combined Atom 1.0 feed from all channels.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Combined Atom 1.0 feed.</returns>
    [HttpGet("/feeds/all.atom")]
    [Produces("application/atom+xml")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> GetAllAtom(CancellationToken cancellationToken)
    {
        return await GetCombinedFeedAsync(FeedType.Audio, "atom", cancellationToken);
    }

    /// <summary>
    /// Serves media files (audio/video/thumbnails) for feed enclosures.
    /// </summary>
    /// <param name="channelId">The channel ID (slug).</param>
    /// <param name="type">The media type (audio, video, thumbnails).</param>
    /// <param name="filename">The filename.</param>
    /// <returns>The media file.</returns>
    [HttpGet("/feeds/{channelId}/{type}/{filename}")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMedia(string channelId, string type, string filename)
    {
        // Validate type
        if (!IsValidMediaType(type))
        {
            _logger.LogWarning("Invalid media type requested: {Type} for channel {ChannelId}", type, channelId);
            return NotFound();
        }

        // Validate extension matches type
        var extension = Path.GetExtension(filename);
        if (!IsValidExtensionForType(extension, type))
        {
            _logger.LogWarning("Invalid extension {Extension} for type {Type}", extension, type);
            return BadRequest();
        }

        // Get the MIME type
        var mimeType = GetMimeType(extension);
        if (mimeType == null)
        {
            _logger.LogWarning("Unknown MIME type for extension {Extension}", extension);
            return BadRequest();
        }

        // TODO: Implement actual file serving with physical file storage
        // This would involve:
        // 1. Looking up the episode by video_id (from filename without extension)
        // 2. Getting the physical file path from the episode record
        // 3. Serving the file with proper range support for streaming
        // 4. Handling conditional requests (If-None-Match, If-Range, etc.)
        
        // For now, return NotFound
        // In a real implementation, this would serve the file:
        // var filePath = await _mediaService.GetMediaPathAsync(channelId, type, filename);
        // return PhysicalFile(filePath, mimeType);

        _logger.LogDebug("Media request: channel={ChannelId}, type={Type}, file={Filename}", 
            channelId, type, filename);
        
        return NotFound();
    }

    /// <summary>
    /// Gets a channel feed with caching and conditional request support.
    /// </summary>
    private async Task<IActionResult> GetChannelFeedAsync(
        string channelId,
        FeedType feedType,
        string feedContentType,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"channel:{channelId}:{feedContentType}";

        FeedGenerationResult? feedResult;
        try
        {
            feedResult = await _feedCache.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    var result = await _feedService.GenerateFeedAsync(channelId, feedType, cancellationToken);
                    return result ?? throw new InvalidOperationException("Channel not found");
                },
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Channel not found - feed service returned null
            return NotFound();
        }

        if (feedResult == null)
        {
            return NotFound();
        }

        // Handle conditional requests
        if (IsNotModified(feedResult))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        SetCacheHeaders(feedResult);
        SetContentType(feedContentType);

        return Content(feedResult.XmlContent, GetContentTypeString(feedContentType));
    }

    /// <summary>
    /// Gets a combined feed with caching and conditional request support.
    /// </summary>
    private async Task<IActionResult> GetCombinedFeedAsync(
        FeedType feedType,
        string feedContentType,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"combined:{feedContentType}";

        var feedResult = await _feedCache.GetOrCreateAsync(
            cacheKey,
            () => _combinedFeedService.GenerateCombinedFeedAsync(feedType, cancellationToken),
            cancellationToken);

        // Handle conditional requests
        if (IsNotModified(feedResult))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        SetCacheHeaders(feedResult);
        SetContentType(feedContentType);

        return Content(feedResult.XmlContent, GetContentTypeString(feedContentType));
    }

    /// <summary>
    /// Checks if the client has a cached version that hasn't been modified.
    /// </summary>
    /// <param name="feedResult">The feed result with ETag and LastModified.</param>
    /// <returns>True if the content has not been modified.</returns>
    private bool IsNotModified(FeedGenerationResult feedResult)
    {
        // Check If-None-Match (ETag comparison)
        var etagHeader = Request.Headers.IfNoneMatch.FirstOrDefault();
        if (!string.IsNullOrEmpty(etagHeader))
        {
            // ETag format: "hash" or just hash
            var clientEtag = etagHeader.Trim('"');
            if (string.Equals(clientEtag, feedResult.Etag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check If-Modified-Since (Last-Modified comparison)
        var modifiedSinceHeader = Request.Headers.IfModifiedSince;
        if (modifiedSinceHeader.Count > 0)
        {
            if (DateTimeOffset.TryParse(modifiedSinceHeader, out var modifiedSince))
            {
                if (feedResult.LastModified <= modifiedSince)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Sets the cache headers for the response.
    /// </summary>
    /// <param name="feedResult">The feed result.</param>
    private void SetCacheHeaders(FeedGenerationResult feedResult)
    {
        // Set ETag
        Response.Headers.ETag = $"\"{feedResult.Etag}\"";

        // Set Last-Modified
        Response.Headers.LastModified = feedResult.LastModified.ToString("R");

        // Set Cache-Control: public, max-age=300 (5 minutes)
        // This matches the spec's recommendation for HTTP caching
        Response.Headers.CacheControl = "public, max-age=300, stale-while-revalidate=60";
    }

    /// <summary>
    /// Sets the content type header.
    /// </summary>
    /// <param name="feedContentType">The feed content type (audio, video, atom).</param>
    private void SetContentType(string feedContentType)
    {
        var contentType = GetContentTypeString(feedContentType);
        Response.ContentType = contentType;
    }

    /// <summary>
    /// Gets the content type string for a feed type.
    /// </summary>
    /// <param name="feedContentType">The feed content type.</param>
    /// <returns>The MIME type string.</returns>
    private static string GetContentTypeString(string feedContentType)
    {
        return feedContentType switch
        {
            "atom" => "application/atom+xml; charset=utf-8",
            _ => "application/rss+xml; charset=utf-8" // Both audio and video use RSS
        };
    }

    /// <summary>
    /// Validates if the media type is valid.
    /// </summary>
    /// <param name="type">The media type.</param>
    /// <returns>True if valid.</returns>
    private static bool IsValidMediaType(string type)
    {
        return type.Equals("audio", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("video", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("thumbnails", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates if the file extension is valid for the given media type.
    /// </summary>
    /// <param name="extension">The file extension (with dot).</param>
    /// <param name="type">The media type.</param>
    /// <returns>True if valid.</returns>
    private static bool IsValidExtensionForType(string extension, string type)
    {
        if (!ValidExtensions.TryGetValue(type, out var validExts))
        {
            return false;
        }

        return validExts.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the MIME type for a file extension.
    /// </summary>
    /// <param name="extension">The file extension (with dot).</param>
    /// <returns>The MIME type or null if unknown.</returns>
    private static string? GetMimeType(string extension)
    {
        return ExtensionMimeTypes.TryGetValue(extension, out var mimeType) 
            ? mimeType 
            : null;
    }
}