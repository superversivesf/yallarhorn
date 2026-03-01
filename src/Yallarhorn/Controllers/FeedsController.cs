namespace Yallarhorn.Controllers;

using Microsoft.AspNetCore.Mvc;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;

/// <summary>
/// Controller for serving a simple feed index page.
/// </summary>
[ApiController]
public class FeedsController : ControllerBase
{
    private readonly IChannelRepository _channelRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeedsController"/> class.
    /// </summary>
    public FeedsController(IChannelRepository channelRepository)
    {
        _channelRepository = channelRepository;
    }

    /// <summary>
    /// Returns a simple HTML page listing all available feeds.
    /// </summary>
    [HttpGet("/feeds")]
    [HttpGet("/feeds/index.html")]
    [Produces("text/html")]
    public async Task<IActionResult> GetFeedIndex()
    {
        var channels = await _channelRepository.GetAllAsync();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Yallarhorn Podcast Feeds</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px; }}
        h1 {{ color: #333; }}
        h2 {{ color: #666; margin-top: 30px; }}
        .channel {{ background: #f5f5f5; padding: 15px; margin: 10px 0; border-radius: 8px; }}
        .channel h3 {{ margin: 0 0 10px 0; color: #444; }}
        .feed-links {{ display: flex; gap: 10px; flex-wrap: wrap; }}
        .feed-links a {{ background: #0066cc; color: white; padding: 5px 12px; border-radius: 4px; text-decoration: none; font-size: 14px; }}
        .feed-links a:hover {{ background: #0052a3; }}
        .feed-links a.video {{ background: #cc6600; }}
        .feed-links a.video:hover {{ background: #a35200; }}
        .combined {{ background: #e8f4e8; padding: 15px; border-radius: 8px; margin-top: 20px; }}
        .combined h2 {{ margin: 0 0 15px 0; color: #2d5a2d; }}
        code {{ background: #eee; padding: 2px 6px; border-radius: 3px; font-size: 13px; }}
    </style>
</head>
<body>
    <h1>🎙️ Yallarhorn Podcast Feeds</h1>
    
    <div class=""combined"">
        <h2>📡 Combined Feeds (All Channels)</h2>
        <p>Subscribe to all channels in one feed:</p>
        <div class=""feed-links"">
            <a href=""{baseUrl}/feeds/all.rss"">All Audio RSS</a>
            <a href=""{baseUrl}/feeds/all.atom"">All Audio Atom</a>
            <a href=""{baseUrl}/feeds/all-video.rss"" class=""video"">All Video RSS</a>
        </div>
    </div>

    <h2>📡 Channel Feeds</h2>
";

        foreach (var channel in channels.OrderBy(c => c.Title))
        {
            html += $@"
    <div class=""channel"">
        <h3>{EscapeHtml(channel.Title)}</h3>
        <div class=""feed-links"">
            <a href=""{baseUrl}/feed/{channel.Id}/audio/rss.xml"">Audio RSS</a>
            <a href=""{baseUrl}/feed/{channel.Id}/audio/atom.xml"">Audio Atom</a>
            <a href=""{baseUrl}/feed/{channel.Id}/video/rss.xml"" class=""video"">Video RSS</a>
        </div>
    </div>
";
        }

        html += $@"
</body>
</html>";

        return Content(html, "text/html");
    }

    private static string EscapeHtml(string input)
    {
        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}