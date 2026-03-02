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
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Yallarhorn Podcast Feeds</title>
    <style>
        * {{ box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 900px; margin: 0 auto; padding: 15px; }}
        h1 {{ color: #333; font-size: 1.5rem; margin-bottom: 10px; }}
        h2 {{ color: #666; margin-top: 25px; font-size: 1.1rem; }}
        .channel {{ background: #f5f5f5; padding: 12px; margin: 8px 0; border-radius: 8px; }}
        .channel h3 {{ margin: 0 0 5px 0; color: #444; display: flex; align-items: center; gap: 8px; flex-wrap: wrap; font-size: 1rem; }}
        .episode-count {{ font-size: 0.85rem; color: #888; font-weight: normal; }}
        .feed-links {{ display: flex; gap: 8px; flex-wrap: wrap; margin-top: 10px; align-items: center; }}
        .feed-item {{ display: flex; align-items: center; gap: 3px; }}
        .feed-links a {{ background: #0066cc; color: white; padding: 6px 10px; border-radius: 4px; text-decoration: none; font-size: 0.85rem; }}
        .feed-links a:hover {{ background: #0052a3; }}
        .feed-links a.video {{ background: #cc6600; }}
        .feed-links a.video:hover {{ background: #a35200; }}
        .feed-links a.refresh {{ background: #28a745; cursor: pointer; }}
        .feed-links a.refresh:hover {{ background: #218838; }}
        .copy-btn {{ background: #6c757d; color: white; border: none; padding: 6px 8px; border-radius: 4px; font-size: 0.75rem; cursor: pointer; }}
        .copy-btn:hover {{ background: #5a6268; }}
        .copy-btn.copied {{ background: #28a745; }}
        .combined {{ background: #e8f4e8; padding: 12px; border-radius: 8px; margin-top: 15px; }}
        .combined h2 {{ margin: 0 0 10px 0; color: #2d5a2d; font-size: 1rem; }}
        .combined p {{ margin: 0 0 10px 0; font-size: 0.9rem; }}
        code {{ background: #eee; padding: 2px 6px; border-radius: 3px; font-size: 0.8rem; }}
        .add-channel {{ background: #fff3cd; padding: 15px; border-radius: 8px; margin: 20px 0; border: 1px solid #ffc107; }}
        .add-channel h2 {{ margin: 0 0 12px 0; color: #856404; font-size: 1rem; }}
        .add-channel form {{ display: flex; gap: 10px; flex-wrap: wrap; }}
        .add-channel .form-group {{ display: flex; flex-direction: column; gap: 5px; flex: 1; min-width: 200px; }}
        .add-channel label {{ font-size: 0.85rem; color: #666; font-weight: 500; }}
        .add-channel input[type=""text""] {{ padding: 10px 12px; border: 1px solid #ccc; border-radius: 4px; font-size: 1rem; width: 100%; }}
        .add-channel button {{ background: #28a745; color: white; border: none; padding: 10px 20px; border-radius: 4px; font-size: 0.9rem; cursor: pointer; align-self: flex-end; }}
        .add-channel button:hover {{ background: #218838; }}
        .status {{ font-size: 0.85rem; padding: 8px 12px; border-radius: 4px; margin-top: 10px; }}
        .status.success {{ background: #d4edda; color: #155724; }}
        .status.error {{ background: #f8d7da; color: #721c24; }}
        .toast {{ position: fixed; bottom: 15px; left: 15px; right: 15px; padding: 12px 20px; border-radius: 8px; color: white; font-size: 0.9rem; opacity: 0; transition: opacity 0.3s; z-index: 1000; text-align: center; }}
        .toast.success {{ background: #28a745; opacity: 1; }}
        .toast.error {{ background: #dc3545; opacity: 1; }}
        @media (max-width: 600px) {{
            body {{ padding: 10px; }}
            h1 {{ font-size: 1.3rem; }}
            .feed-links {{ gap: 6px; }}
            .feed-links a {{ padding: 5px 8px; font-size: 0.8rem; }}
            .copy-btn {{ padding: 5px 6px; font-size: 0.7rem; }}
            .add-channel .form-group {{ min-width: 100%; }}
        }}
    </style>
</head>
<body>
    <h1>🎙️ Yallarhorn Feeds</h1>

    <div class=""add-channel"">
        <h2>➕ Add New Channel</h2>
        <form id=""addChannelForm"">
            <div class=""form-group"">
                <label for=""channelUrl"">YouTube Channel URL</label>
                <input type=""text"" id=""channelUrl"" name=""url"" placeholder=""https://www.youtube.com/@ChannelName"" required />
            </div>
            <button type=""submit"">Add</button>
        </form>
        <div id=""addStatus"" class=""status"" style=""display:none;""></div>
    </div>
    
    <div class=""combined"">
        <h2>📡 Combined Feeds (All Channels)</h2>
        <p>Subscribe to all channels:</p>
        <div class=""feed-links"">
            <div class=""feed-item""><a href=""{baseUrl}/feeds/all.rss"">Audio RSS</a><button class=""copy-btn"" onclick=""copyUrl('{baseUrl}/feeds/all.rss', this)"">📋</button></div>
            <div class=""feed-item""><a href=""{baseUrl}/feeds/all.atom"">Audio Atom</a><button class=""copy-btn"" onclick=""copyUrl('{baseUrl}/feeds/all.atom', this)"">📋</button></div>
            <div class=""feed-item""><a href=""{baseUrl}/feeds/all-video.rss"" class=""video"">Video RSS</a><button class=""copy-btn"" onclick=""copyUrl('{baseUrl}/feeds/all-video.rss', this)"">📋</button></div>
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
        <h3>{EscapeHtml(channel.Title)} <span class=""episode-count"">({episodeCount})</span></h3>
        <div class=""feed-links"">
            <div class=""feed-item""><a href=""{baseUrl}/feed/{channel.Id}/audio.rss"">Audio RSS</a><button class=""copy-btn"" onclick=""copyUrl('{baseUrl}/feed/{channel.Id}/audio.rss', this)"">📋</button></div>
            <div class=""feed-item""><a href=""{baseUrl}/feed/{channel.Id}/atom.xml"">Audio Atom</a><button class=""copy-btn"" onclick=""copyUrl('{baseUrl}/feed/{channel.Id}/atom.xml', this)"">📋</button></div>
            <div class=""feed-item""><a href=""{baseUrl}/feed/{channel.Id}/video.rss"" class=""video"">Video RSS</a><button class=""copy-btn"" onclick=""copyUrl('{baseUrl}/feed/{channel.Id}/video.rss', this)"">📋</button></div>
            <a href="""" class=""refresh"" onclick=""refreshChannel('{channel.Id}'); return false;"">🔄</a>
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

        function copyUrl(url, btn) {
            navigator.clipboard.writeText(url).then(() => {
                btn.textContent = '✓';
                btn.classList.add('copied');
                setTimeout(() => {
                    btn.textContent = '📋';
                    btn.classList.remove('copied');
                }, 1500);
            }).catch(() => {
                showToast('Failed to copy', false);
            });
        }

        function refreshChannel(channelId) {
            fetch('/api/v1/channels/' + channelId + '/refresh', { method: 'POST' })
                .then(response => response.ok ? showToast('Refresh started', true) : showToast('Failed to refresh', false))
                .catch(() => showToast('Error refreshing', false));
        }

        function refreshAll() {
            fetch('/api/v1/refresh', { method: 'POST' })
                .then(response => response.ok ? showToast('Refreshing all channels', true) : showToast('Failed to refresh', false))
                .catch(() => showToast('Error refreshing', false));
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
                    statusDiv.textContent = 'Channel added! Refreshing...';
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