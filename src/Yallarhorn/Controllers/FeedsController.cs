namespace Yallarhorn.Controllers;

using Microsoft.AspNetCore.Mvc;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Services;

/// <summary>
/// Controller for serving a simple feed index page.
/// </summary>
[ApiController]
public class FeedsController : ControllerBase
{
    private readonly IChannelRepository _channelRepository;
    private readonly IEpisodeRepository _episodeRepository;
    private readonly IVersionService _versionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeedsController"/> class.
    /// </summary>
    public FeedsController(IChannelRepository channelRepository, IEpisodeRepository episodeRepository, IVersionService versionService)
    {
        _channelRepository = channelRepository;
        _episodeRepository = episodeRepository;
        _versionService = versionService;
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
        var version = _versionService.GetVersion();

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
        .channel {{ background: #f5f5f5; padding: 12px; margin: 8px 0; border-radius: 8px; display: flex; align-items: flex-start; gap: 12px; flex-wrap: wrap; }}
        .channel-thumb {{ width: 48px; height: 48px; border-radius: 50%; object-fit: cover; flex-shrink: 0; }}
        .channel-content {{ flex: 1; min-width: 0; }}
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
        .feed-links a.delete-btn {{ background: #dc3545; cursor: pointer; color: white; padding: 6px 10px; border-radius: 4px; text-decoration: none; font-size: 0.85rem; }}
        .feed-links a.delete-btn:hover {{ background: #c82333; }}
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
        .add-channel input[type=""number""] {{ padding: 10px 12px; border: 1px solid #ccc; border-radius: 4px; font-size: 1rem; width: 100%; }}
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
        @keyframes spin {{
            0% {{ transform: rotate(0deg); }}
            100% {{ transform: rotate(360deg); }}
        }}
        .spinner {{
            display: inline-block;
            width: 14px;
            height: 14px;
            border: 2px solid #ccc;
            border-top-color: #0066cc;
            border-radius: 50%;
            animation: spin 1s linear infinite;
            margin-right: 5px;
            vertical-align: middle;
        }}
        .sync-status {{
            font-size: 0.8rem;
            color: #888;
            margin-left: 8px;
            display: inline-flex;
            align-items: center;
        }}
        .syncing-text {{
            color: #0066cc;
            font-weight: 500;
        }}
        .modal-overlay {{
            position: fixed;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background: rgba(0, 0, 0, 0.5);
            display: flex;
            align-items: center;
            justify-content: center;
            z-index: 2000;
            opacity: 0;
            visibility: hidden;
            transition: opacity 0.2s, visibility 0.2s;
        }}
        .modal-overlay.active {{
            opacity: 1;
            visibility: visible;
        }}
        .modal {{
            background: white;
            border-radius: 8px;
            padding: 24px;
            max-width: 400px;
            width: 90%;
            box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15);
        }}
        .modal h3 {{
            margin: 0 0 12px 0;
            color: #333;
            font-size: 1.1rem;
        }}
        .modal p {{
            margin: 0 0 20px 0;
            color: #666;
            font-size: 0.95rem;
            line-height: 1.5;
        }}
        .modal-buttons {{
            display: flex;
            gap: 12px;
            justify-content: flex-end;
        }}
        .modal-buttons button {{
            padding: 8px 16px;
            border-radius: 4px;
            font-size: 0.9rem;
            cursor: pointer;
            border: none;
            font-weight: 500;
        }}
        .modal-cancel {{
            background: #f5f5f5;
            color: #333;
        }}
        .modal-cancel:hover {{
            background: #e0e0e0;
        }}
        .modal-confirm {{
            background: #dc3545;
            color: white;
        }}
        .modal-confirm:hover {{
            background: #c82333;
        }}
        .modal-buttons button:focus {{
            outline: 3px solid #0066cc;
            outline-offset: 2px;
        }}
    </style>
</head>
<body>
    <h1>🎙️ Yallarhorn Feeds [{version}]</h1>

    <div class=""add-channel"">
        <h2>➕ Add New Channel</h2>
        <form id=""addChannelForm"">
            <div class=""form-group"">
                <label for=""channelUrl"">YouTube Channel URL</label>
                <input type=""text"" id=""channelUrl"" name=""url"" placeholder=""https://www.youtube.com/@ChannelName"" required />
            </div>
            <div class=""form-group"">
                <label for=""channelTitle"">Title (Optional)</label>
                <input type=""text"" id=""channelTitle"" name=""title"" placeholder=""Custom title override"" />
            </div>
            <div class=""form-group"" style=""min-width: 120px; max-width: 180px;"">
                <label for=""episodeCount"">Episodes to Download</label>
                <input type=""number"" id=""episodeCount"" name=""episodeCount"" min=""1"" max=""100"" value=""3"" required />
            </div>
            <div class=""form-group"" style=""min-width: 120px; max-width: 150px;"">
                <label for=""feedType"">Feed Type</label>
                <select id=""feedType"" name=""feedType"" style=""padding: 10px 12px; border: 1px solid #ccc; border-radius: 4px; font-size: 1rem;"">
                    <option value=""video"" selected>Video</option>
                    <option value=""audio"">Audio</option>
                    <option value=""both"">Both</option>
                </select>
            </div>
            <div class=""form-group"" style=""min-width: 120px; max-width: 150px;"">
                <label for=""enabled"">Enabled</label>
                <div style=""display: flex; align-items: center; height: 42px;"">
                    <input type=""checkbox"" id=""enabled"" name=""enabled"" checked style=""transform: scale(1.5);"" />
                </div>
            </div>
            <button type=""submit"">Add</button>
        </form>
        <div id=""addStatus"" class=""status"" style=""display:none;""></div>
    </div>
    
    <div class=""queue-status"" style=""background: #e3f2fd; padding: 15px; border-radius: 8px; margin: 20px 0; border: 1px solid #90caf9;"">
        <h2 style=""margin: 0 0 10px 0; color: #1565c0; font-size: 1rem;"">📥 Download Queue</h2>
        <div id=""queueContent"">
            <p style=""margin: 0; color: #666; font-size: 0.9rem;"">Checking queue status...</p>
        </div>
    </div>
    
    <div class=""combined"">
        <h2>📡 Combined Feeds (All Channels)</h2>
        <p>Subscribe to all channels:</p>
        <div class=""feed-links"">
            <div class=""feed-item""><a href=""{baseUrl}/feeds/all.atom"">Atom</a><button class=""copy-btn"" onclick=""copyUrl('{baseUrl}/feeds/all.atom', this)"">📋</button></div>
            <div class=""feed-item""><a href=""{baseUrl}/feeds/all-video.rss"" class=""video"">RSS</a><button class=""copy-btn"" onclick=""copyUrl('{baseUrl}/feeds/all-video.rss', this)"">📋</button></div>
            <a href="""" class=""refresh"" onclick=""refreshAll(); return false;"">🔄 Refresh All</a>
        </div>
    </div>

    <h2>📡 Channel Feeds</h2>
";

        foreach (var channel in channels.OrderBy(c => c.Title))
        {
            var episodeCount = await _episodeRepository.CountByChannelIdAsync(channel.Id);
            var thumbnailHtml = !string.IsNullOrEmpty(channel.ThumbnailUrl)
                ? $"<img src=\"{EscapeHtml(channel.ThumbnailUrl)}\" alt=\"{EscapeHtml(channel.Title)}\" class=\"channel-thumb\" onerror=\"this.style.display='none'\" />"
                : "";
            var lastRefreshStr = channel.LastRefreshAt?.ToString("o") ?? "";
            html += $@"
    <div class=""channel"" id=""channel-{channel.Id}"" data-channel-id=""{channel.Id}"" data-last-refresh=""{lastRefreshStr}"">
        {thumbnailHtml}
        <div class=""channel-content"">
            <h3>{EscapeHtml(channel.Title)} <span class=""episode-count"">({episodeCount})</span><span class=""sync-status"" id=""sync-status-{channel.Id}""></span></h3>
            <div class=""feed-links"">
                <div class=""feed-item""><a href=""{baseUrl}/feed/{channel.Id}/atom.xml"">Atom</a><button class=""copy-btn"" onclick=""copyUrl('{baseUrl}/feed/{channel.Id}/atom.xml', this)"">📋</button></div>
                <div class=""feed-item""><a href=""{baseUrl}/feed/{channel.Id}/video.rss"" class=""video"">RSS</a><button class=""copy-btn"" onclick=""copyUrl('{baseUrl}/feed/{channel.Id}/video.rss', this)"">📋</button></div>
                <a href="""" class=""refresh"" onclick=""refreshChannel('{channel.Id}'); return false;"">🔄</a>
                <a href="""" class=""delete-btn"" onclick=""deleteChannel('{channel.Id}', this); return false;"">🗑️</a>
            </div>
        </div>
    </div>
";
        }

        html += @"
    <div id=""toast"" class=""toast""></div>
    
    <div id=""deleteModal"" class=""modal-overlay"" role=""dialog"" aria-modal=""true"" aria-labelledby=""modalTitle"" aria-describedby=""modalDesc"">
        <div class=""modal"">
            <h3 id=""modalTitle"">Delete Channel?</h3>
            <p id=""modalDesc"">This will permanently delete the channel and all its episodes. This action cannot be undone.</p>
            <div class=""modal-buttons"">
                <button type=""button"" class=""modal-cancel"" onclick=""closeDeleteModal();"">Cancel</button>
                <button type=""button"" class=""modal-confirm"" id=""confirmDeleteBtn"">Delete</button>
            </div>
        </div>
    </div>

    <script>
        function showToast(message, isSuccess) {
            const toast = document.getElementById('toast');
            toast.textContent = message;
            toast.className = 'toast ' + (isSuccess ? 'success' : 'error');
            setTimeout(() => { toast.className = 'toast'; }, 3000);
        }

        function copyUrl(url, btn) {
            // Try modern clipboard API first
            if (navigator.clipboard && window.isSecureContext) {
                navigator.clipboard.writeText(url).then(() => {
                    showCopySuccess(btn);
                }).catch(() => {
                    fallbackCopy(url, btn);
                });
            } else {
                // Fallback for non-HTTPS contexts
                fallbackCopy(url, btn);
            }
        }

        function fallbackCopy(url, btn) {
            const textArea = document.createElement('textarea');
            textArea.value = url;
            textArea.style.position = 'fixed';
            textArea.style.left = '-999999px';
            textArea.style.top = '-999999px';
            document.body.appendChild(textArea);
            textArea.focus();
            textArea.select();
            try {
                document.execCommand('copy');
                showCopySuccess(btn);
            } catch (err) {
                showToast('Failed to copy URL', false);
            }
            document.body.removeChild(textArea);
        }

        function showCopySuccess(btn) {
            btn.textContent = '✓';
            btn.classList.add('copied');
            setTimeout(() => {
                btn.textContent = '📋';
                btn.classList.remove('copied');
            }, 1500);
        }

        function refreshChannel(channelId) {
            fetch('/api/v1/channels/' + channelId + '/refresh', { method: 'POST' })
                .then(response => response.ok ? showToast('Refresh started', true) : showToast('Failed to refresh', false))
                .catch(() => showToast('Error refreshing', false));
        }

        function refreshAll() {
            fetch('/api/v1/refresh-all', { method: 'POST' })
                .then(response => response.ok ? showToast('Refreshing all channels', true) : showToast('Failed to refresh', false))
                .catch(() => showToast('Error refreshing', false));
        }

        var pendingChannelId = null;
        var pendingButton = null;

        function showDeleteModal(channelId, btn) {
            pendingChannelId = channelId;
            pendingButton = btn;
            const modal = document.getElementById('deleteModal');
            modal.classList.add('active');
            document.getElementById('confirmDeleteBtn').focus();
        }

        function closeDeleteModal() {
            const modal = document.getElementById('deleteModal');
            modal.classList.remove('active');
            pendingChannelId = null;
            pendingButton = null;
        }

        function confirmDelete() {
            if (!pendingChannelId || !pendingButton) {
                return;
            }
            const channelId = pendingChannelId;
            const btn = pendingButton;
            closeDeleteModal();
            
            btn.textContent = 'Deleting...';
            fetch('/api/v1/channels/' + channelId, { method: 'DELETE' })
                .then(response => {
                    if (response.ok) {
                        showToast('Channel deleted', true);
                        const div = document.getElementById('channel-' + channelId);
                        if (div) div.remove();
                    } else {
                        btn.textContent = '🗑️';
                        showToast('Failed to delete', false);
                    }
                })
                .catch(() => {
                    btn.textContent = '🗑️';
                    showToast('Error deleting', false);
                });
        }

        function deleteChannel(channelId, btn) {
            showDeleteModal(channelId, btn);
        }

        // Handle escape key to close modal
        document.addEventListener('keydown', function(e) {
            const modal = document.getElementById('deleteModal');
            if (e.key === 'Escape' && modal.classList.contains('active')) {
                closeDeleteModal();
            }
        });

        // Setup confirm button
        document.getElementById('confirmDeleteBtn').addEventListener('click', confirmDelete);

        document.getElementById('addChannelForm').addEventListener('submit', function(e) {
            e.preventDefault();
            
            const urlInput = document.getElementById('channelUrl');
            const titleInput = document.getElementById('channelTitle');
            const episodeCountInput = document.getElementById('episodeCount');
            const feedTypeInput = document.getElementById('feedType');
            const enabledInput = document.getElementById('enabled');
            const submitBtn = e.target.querySelector('button[type=""submit""]');
            const statusDiv = document.getElementById('addStatus');
            
            const originalBtnText = submitBtn.textContent;
            
            function setLoading(loading) {
                submitBtn.disabled = loading;
                urlInput.disabled = loading;
                titleInput.disabled = loading;
                episodeCountInput.disabled = loading;
                feedTypeInput.disabled = loading;
                enabledInput.disabled = loading;
                
                if (loading) {
                    submitBtn.innerHTML = '<span class=""spinner""></span>Adding...';
                    statusDiv.className = 'status';
                    statusDiv.innerHTML = '<span class=""spinner""></span>Fetching channel info from YouTube...';
                    statusDiv.style.display = 'block';
                } else {
                    submitBtn.textContent = originalBtnText;
                }
            }
            
            setLoading(true);
            
            const requestBody = {
                url: urlInput.value,
                episode_count_config: parseInt(episodeCountInput.value) || 3,
                feed_type: feedTypeInput.value,
                enabled: enabledInput.checked
            };
            
            const title = titleInput.value;
            if (title) {
                requestBody.title = title;
            }
            
            fetch('/api/v1/channels', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(requestBody)
            })
            .then(response => {
                if (response.ok) {
                    statusDiv.className = 'status success';
                    statusDiv.textContent = 'Channel added! Refreshing...';
                    setTimeout(() => location.reload(), 1000);
                    return null;
                }
                
                const contentType = response.headers.get('content-type');
                if (contentType && contentType.includes('application/json')) {
                    return response.json().then(data => {
                        throw new Error(data.message || data.error || 'Failed to add channel');
                    });
                } else {
                    return response.text().then(text => {
                        throw new Error('Server error: ' + response.status + ' ' + response.statusText);
                    });
                }
            })
            .catch(error => {
                setLoading(false);
                statusDiv.className = 'status error';
                statusDiv.textContent = error.message || 'An unexpected error occurred';
                statusDiv.style.display = 'block';
            });
        });

        function updateQueueStatus() {
            fetch('/api/v1/queue')
                .then(response => response.json())
                .then(data => {
                    var container = document.getElementById('queueContent');
                    
                    if (!data || (!data.activeDownloads || data.activeDownloads.length === 0) && !data.pendingCount) {
                        container.innerHTML = '<p style=""margin: 0; color: #666; font-size: 0.9rem;"">No active downloads</p>';
                        return;
                    }
                    
                    var html = '';
                    
                    if (data.activeDownloads && data.activeDownloads.length > 0) {
                        html += '<div style=""margin-bottom: 10px;"">';
                        html += '<div style=""font-size: 0.85rem; color: #333; margin-bottom: 5px;"">Active Downloads:</div>';
                        
                        data.activeDownloads.forEach(function(dl) {
                            var progress = dl.progressPercent || 0;
                            var title = dl.episodeTitle || 'Unknown';
                            var channel = dl.channelName || 'Unknown';
                            html += '<div style=""background: #fff; padding: 8px 10px; border-radius: 4px; margin: 5px 0; font-size: 0.85rem;"">';
                            html += '<div style=""font-weight: 500; color: #333;"">' + title + '</div>';
                            html += '<div style=""color: #666; font-size: 0.8rem;"">' + channel + '</div>';
                            html += '<div style=""background: #e0e0e0; border-radius: 3px; margin-top: 4px; height: 6px; overflow: hidden;"">';
                            html += '<div style=""background: #28a745; height: 100%; width: ' + progress + '%;""></div></div>';
                            html += '<div style=""color: #888; font-size: 0.75rem; margin-top: 2px;"">' + progress.toFixed(1) + '%</div></div>';
                        });
                        html += '</div>';
                    }
                    
                    if (data.pendingCount > 0) {
                        html += '<div style=""font-size: 0.85rem; color: #666;"">';
                        html += '<span style=""font-weight: 500;"">' + data.pendingCount + '</span> download(s) pending</div>';
                    }
                    
                    container.innerHTML = html;
                })
                .catch(function() {
                    var container = document.getElementById('queueContent');
                    container.innerHTML = '<p style=""margin: 0; color: #666; font-size: 0.9rem;"">Unable to fetch queue status</p>';
                });
        }

        // Initial queue status check
        updateQueueStatus();
        // Auto-refresh every 10 seconds
        setInterval(updateQueueStatus, 10000);

        function updateSyncStatus() {
            fetch('/api/v1/channels/sync-status')
                .then(response => response.json())
                .then(data => {
                    data.forEach(function(item) {
                        var statusEl = document.getElementById('sync-status-' + item.channelId);
                        var channelEl = document.getElementById('channel-' + item.channelId);
                        
                        if (!statusEl || !channelEl) return;
                        
                        if (item.isSyncing) {
                            statusEl.innerHTML = '<span class=""spinner""></span><span class=""syncing-text"">Syncing...</span>';
                        } else {
                            var lastRefresh = channelEl.getAttribute('data-last-refresh');
                            if (lastRefresh) {
                                var lastDate = new Date(lastRefresh);
                                var now = new Date();
                                var diffMs = now - lastDate;
                                var diffMins = Math.floor(diffMs / 60000);
                                var diffHours = Math.floor(diffMins / 60);
                                var diffDays = Math.floor(diffHours / 24);
                                
                                var timeText = '';
                                if (diffDays > 0) {
                                    timeText = diffDays + ' day' + (diffDays > 1 ? 's' : '') + ' ago';
                                } else if (diffHours > 0) {
                                    timeText = diffHours + ' hour' + (diffHours > 1 ? 's' : '') + ' ago';
                                } else if (diffMins > 0) {
                                    timeText = diffMins + ' minute' + (diffMins > 1 ? 's' : '') + ' ago';
                                } else {
                                    timeText = 'just now';
                                }
                                statusEl.innerHTML = 'Last synced: ' + timeText;
                            } else {
                                statusEl.innerHTML = 'Never synced';
                            }
                        }
                    });
                })
                .catch(function() {
                    // Silently fail - sync status is not critical
                });
        }

        // Initial sync status check
        updateSyncStatus();
        // Poll every 5 seconds
        setInterval(updateSyncStatus, 5000);
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