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
    private readonly IEpisodeRepository _episodeRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeedsController"/> class.
    /// </summary>
    public FeedsController(IChannelRepository channelRepository, IEpisodeRepository episodeRepository)
    {
        _channelRepository = channelRepository;
        _episodeRepository = episodeRepository;
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
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 900px; margin: 0 auto; padding: 20px; }}
        h1 {{ color: #333; }}
        h2 {{ color: #666; margin-top: 30px; }}
        .channel {{ background: #f5f5f5; padding: 15px; margin: 10px 0; border-radius: 8px; position: relative; }}
        .channel h3 {{ margin: 0 0 5px 0; color: #444; display: flex; align-items: center; gap: 10px; }}
        .episode-count {{ font-size: 14px; color: #888; font-weight: normal; }}
        .feed-links {{ display: flex; gap: 10px; flex-wrap: wrap; margin-top: 10px; }}
        .feed-links a {{ background: #0066cc; color: white; padding: 5px 12px; border-radius: 4px; text-decoration: none; font-size: 14px; }}
        .feed-links a:hover {{ background: #0052a3; }}
        .feed-links a.video {{ background: #cc6600; }}
        .feed-links a.video:hover {{ background: #a35200; }}
        .feed-links a.refresh {{ background: #28a745; cursor: pointer; }}
        .feed-links a.refresh:hover {{ background: #218838; }}
        .combined {{ background: #e8f4e8; padding: 15px; border-radius: 8px; margin-top: 20px; }}
        .combined h2 {{ margin: 0 0 15px 0; color: #2d5a2d; }}
        code {{ background: #eee; padding: 2px 6px; border-radius: 3px; font-size: 13px; }}
        .add-channel {{ background: #fff3cd; padding: 20px; border-radius: 8px; margin: 30px 0; border: 1px solid #ffc107; }}
        .add-channel h2 {{ margin: 0 0 15px 0; color: #856404; }}
        .add-channel form {{ display: flex; gap: 10px; flex-wrap: wrap; align-items: flex-end; }}
        .add-channel .form-group {{ display: flex; flex-direction: column; gap: 5px; }}
        .add-channel label {{ font-size: 14px; color: #666; font-weight: 500; }}
        .add-channel input[type=""text""] {{ padding: 8px 12px; border: 1px solid #ccc; border-radius: 4px; font-size: 14px; min-width: 350px; }}
        .add-channel button {{ background: #28a745; color: white; border: none; padding: 8px 20px; border-radius: 4px; font-size: 14px; cursor: pointer; }}
        .add-channel button:hover {{ background: #218838; }}
        .status {{ font-size: 14px; padding: 5px 10px; border-radius: 4px; margin-top: 10px; }}
        .status.success {{ background: #d4edda; color: #155724; }}
        .status.error {{ background: #f8d7da; color: #721c24; }}
        .toast {{ position: fixed; bottom: 20px; right: 20px; padding: 15px 25px; border-radius: 8px; color: white; font-size: 14px; opacity: 0; transition: opacity 0.3s; z-index: 1000; }}
        .toast.success {{ background: #28a745; opacity: 1; }}
        .toast.error {{ background: #dc3545; opacity: 1; }}
    </style>
</head>
<body>
    <h1>🎙️ Yallarhorn Podcast Feeds</h1>

    <div class=""add-channel"">
        <h2>➕ Add New Channel</h2>
        <form id=""addChannelForm"">
            <div class=""form-group"">
                <label for=""channelUrl"">YouTube Channel URL</label>
                <input type=""text"" id=""channelUrl"" name=""url"" placeholder=""https://www.youtube.com/@ChannelName"" required />
            </div>
            <button type=""submit"">Add Channel</button>
        </form>
        <div id=""addStatus"" class=""status"" style=""display:none;""></div>
    </div>
    
    <div class=""combined"">
        <h2>📡 Combined Feeds (All Channels)</h2>
        <p>Subscribe to all channels in one feed:</p>
        <div class=""feed-links"">
            <a href=""{baseUrl}/feeds/all.rss"">All Audio RSS</a>
            <a href=""{baseUrl}/feeds/all.atom"">All Audio Atom</a>
            <a href=""{baseUrl}/feeds/all-video.rss"" class=""video"">All Video RSS</a>
            <a href="""" class=""refresh"" onclick=""refreshAll(); return false;"">🔄 Refresh All</a>
        </div>
    </div>

    <h2>📡 Channel Feeds</h2>
";

        foreach (var channel in channels.OrderBy(c => c.Title))
        {
            var episodeCount = await _episodeRepository.CountByChannelIdAsync(channel.Id);
            html += $@"
    <div class=""channel"" id=""channel-{channel.Id}"">
        <h3>{EscapeHtml(channel.Title)} <span class=""episode-count"">({episodeCount} episodes)</span></h3>
        <div class=""feed-links"">
            <a href=""{baseUrl}/feed/{channel.Id}/audio.rss"">Audio RSS</a>
            <a href=""{baseUrl}/feed/{channel.Id}/atom.xml"">Audio Atom</a>
            <a href=""{baseUrl}/feed/{channel.Id}/video.rss"" class=""video"">Video RSS</a>
            <a href="""" class=""refresh"" onclick=""refreshChannel('{channel.Id}'); return false;"">🔄 Refresh</a>
        </div>
    </div>
";
        }

        html += @"
    <div id=""toast"" class=""toast""></div>

    <script>
        function showToast(message, isSuccess) {
            const toast = document.getElementById('toast');
            toast.textContent = message;
            toast.className = 'toast ' + (isSuccess ? 'success' : 'error');
            setTimeout(() => { toast.className = 'toast'; }, 3000);
        }

        function refreshChannel(channelId) {
            fetch('/api/v1/channels/' + channelId + '/refresh', { method: 'POST' })
                .then(response => response.ok ? showToast('Channel refresh started', true) : showToast('Failed to refresh channel', false))
                .catch(() => showToast('Error refreshing channel', false));
        }

        function refreshAll() {
            fetch('/api/v1/refresh', { method: 'POST' })
                .then(response => response.ok ? showToast('Refresh all channels started', true) : showToast('Failed to refresh', false))
                .catch(() => showToast('Error refreshing channels', false));
        }

        document.getElementById('addChannelForm').addEventListener('submit', function(e) {
            e.preventDefault();
            const url = document.getElementById('channelUrl').value;
            const statusDiv = document.getElementById('addStatus');
            
            fetch('/api/v1/channels', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ url: url, episode_count_config: 50, feed_type: 'both', enabled: true })
            })
            .then(response => {
                if (response.ok) {
                    statusDiv.className = 'status success';
                    statusDiv.textContent = 'Channel added successfully! Refreshing...';
                    statusDiv.style.display = 'block';
                    setTimeout(() => location.reload(), 1500);
                } else {
                    return response.json().then(data => {
                        throw new Error(data.message || 'Failed to add channel');
                    });
                }
            })
            .catch(error => {
                statusDiv.className = 'status error';
                statusDiv.textContent = error.message;
                statusDiv.style.display = 'block';
            });
        });
    </script>
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