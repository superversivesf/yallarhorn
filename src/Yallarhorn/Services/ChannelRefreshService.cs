namespace Yallarhorn.Services;

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Models;

/// <summary>
/// Result of a channel refresh operation.
/// </summary>
public record RefreshResult
{
    /// <summary>
    /// Gets the channel ID that was refreshed.
    /// </summary>
    public required string ChannelId { get; init; }

    /// <summary>
    /// Gets the number of videos found in the channel.
    /// </summary>
    public int VideosFound { get; init; }

    /// <summary>
    /// Gets the number of new episodes queued for download.
    /// </summary>
    public int EpisodesQueued { get; init; }

    /// <summary>
    /// Gets the timestamp when the refresh completed.
    /// </summary>
    public DateTimeOffset? RefreshedAt { get; init; }

    /// <summary>
    /// Gets the error message if the refresh failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Interface for channel refresh service operations.
/// </summary>
public interface IChannelRefreshService
{
    /// <summary>
    /// Refreshes a single channel, discovering new episodes.
    /// </summary>
    /// <param name="channelId">The channel ID to refresh.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refresh result.</returns>
    Task<RefreshResult> RefreshChannelAsync(string channelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes all enabled channels.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of refresh results for each channel.</returns>
    Task<IEnumerable<RefreshResult>> RefreshAllChannelsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for discovering new episodes from YouTube channels.
/// </summary>
public class ChannelRefreshService : IChannelRefreshService
{
    private readonly IChannelRepository _channelRepository;
    private readonly IEpisodeRepository _episodeRepository;
    private readonly IYtDlpClient _ytDlpClient;
    private readonly IDownloadQueueService _queueService;
    private readonly ILogger<ChannelRefreshService>? _logger;
    private readonly string _downloadDirectory;
    
    /// <summary>
    /// Static dictionary to track which channels are currently syncing.
    /// Key: ChannelId, Value: true if syncing
    /// </summary>
    private static readonly ConcurrentDictionary<string, bool> _syncingChannels = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelRefreshService"/> class.
    /// </summary>
    /// <param name="channelRepository">The channel repository.</param>
    /// <param name="episodeRepository">The episode repository.</param>
    /// <param name="ytDlpClient">The yt-dlp client.</param>
    /// <param name="queueService">The download queue service.</param>
    /// <param name="downloadDirectory">Directory for downloaded content.</param>
    /// <param name="logger">Optional logger.</param>
    public ChannelRefreshService(
        IChannelRepository channelRepository,
        IEpisodeRepository episodeRepository,
        IYtDlpClient ytDlpClient,
        IDownloadQueueService queueService,
        string? downloadDirectory = null,
        ILogger<ChannelRefreshService>? logger = null)
    {
        _channelRepository = channelRepository;
        _episodeRepository = episodeRepository;
        _ytDlpClient = ytDlpClient;
        _queueService = queueService;
        _downloadDirectory = downloadDirectory ?? "./downloads";
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RefreshResult> RefreshChannelAsync(string channelId, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Refreshing channel {ChannelId}", channelId);

        // Step 1: Get the channel
        var channel = await _channelRepository.GetByIdAsync(channelId, cancellationToken);
        if (channel == null)
        {
            _logger?.LogWarning("Channel {ChannelId} not found", channelId);
            return new RefreshResult
            {
                ChannelId = channelId,
                VideosFound = 0,
                EpisodesQueued = 0,
                RefreshedAt = null
            };
        }

        // Mark channel as syncing
        _syncingChannels[channelId] = true;

        try
        {
            // Step 2: Fetch video list from YouTube via yt-dlp
            _logger?.LogDebug("Fetching videos for channel {ChannelId} from {Url}", channel.Id, channel.Url);
            var videos = await _ytDlpClient.GetChannelVideosAsync(channel.Url, cancellationToken);
            var videoList = videos.ToList();

            _logger?.LogInformation("Found {Count} videos for channel {ChannelId}", videoList.Count, channel.Id);

            if (videoList.Count == 0)
            {
                _logger?.LogWarning("No videos returned from yt-dlp for channel {ChannelId} at {Url}", channel.Id, channel.Url);
            }

            // Step 3: Filter by rolling window (top N by published_at)
            var videosToProcess = videoList
                .OrderByDescending(v => v.Timestamp ?? 0) // Sort by published_at descending, null timestamps go last
                .Take(channel.EpisodeCountConfig)
                .ToList();

            _logger?.LogDebug(
                "Processing {Count} videos (rolling window of {MaxVideos}) for channel {ChannelId}",
                videosToProcess.Count,
                channel.EpisodeCountConfig,
                channel.Id);

            // Step 4: Check for new episodes (dedupe by video_id) and create
            var episodesQueued = 0;
            foreach (var video in videosToProcess)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check if episode already exists
                var existing = await _episodeRepository.GetByVideoIdAsync(video.Id, cancellationToken);
                if (existing != null)
                {
                    _logger?.LogDebug(
                        "Episode {VideoId} already exists (status: {Status}), skipping",
                        video.Id,
                        existing.Status);
                    continue;
                }

                // Create new episode
                var episode = await CreateEpisodeAsync(channel, video, cancellationToken);

                // Queue for download
                await _queueService.EnqueueAsync(episode.Id, priority: 5, cancellationToken);
                episodesQueued++;

                _logger?.LogInformation(
                    "Created and queued new episode {EpisodeId} (VideoId: {VideoId}) for channel {ChannelId}",
                    episode.Id,
                    video.Id,
                    channel.Id);
            }

            // Step 5: Download channel avatar (non-critical, don't fail if this fails)
            try
            {
                await DownloadChannelArtworkAsync(channel, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to download channel avatar for {ChannelId}", channel.Id);
            }

            // Step 6: Update channel's last_refresh_at timestamp
            var refreshedAt = DateTimeOffset.UtcNow;
            channel.LastRefreshAt = refreshedAt;
            channel.UpdatedAt = refreshedAt;
            await _channelRepository.UpdateAsync(channel, cancellationToken);

            _logger?.LogInformation(
                "Channel {ChannelId} refresh complete: {VideosFound} videos found, {EpisodesQueued} episodes queued",
                channel.Id,
                videoList.Count,
                episodesQueued);

            return new RefreshResult
            {
                ChannelId = channel.Id,
                VideosFound = videoList.Count,
                EpisodesQueued = episodesQueued,
                RefreshedAt = refreshedAt
            };
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Channel {ChannelId} refresh was cancelled", channel.Id);
            throw;
        }
        catch (YtDlpException ex)
        {
            _logger?.LogError(ex, "Failed to fetch videos for channel {ChannelId}: {Message}", channel.Id, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error refreshing channel {ChannelId}: {Message}", channel.Id, ex.Message);
            throw;
        }
        finally
        {
            // Clear sync state when done (success or failure)
            _syncingChannels.TryRemove(channelId, out _);
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RefreshResult>> RefreshAllChannelsAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting refresh of all enabled channels");

        // Get all enabled channels
        var channels = await _channelRepository.GetEnabledAsync(cancellationToken);
        var channelList = channels.ToList();

        _logger?.LogInformation("Found {Count} enabled channels to refresh", channelList.Count);

        var results = new List<RefreshResult>();

        foreach (var channel in channelList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await RefreshChannelAsync(channel.Id, cancellationToken);
                results.Add(result);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Refresh all channels was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to refresh channel {ChannelId}, continuing with next channel", channel.Id);
                // Continue with other channels - don't fail the entire operation
                results.Add(new RefreshResult
                {
                    ChannelId = channel.Id,
                    VideosFound = 0,
                    EpisodesQueued = 0,
                    RefreshedAt = null,
                    ErrorMessage = ex.Message
                });
            }
        }

        _logger?.LogInformation(
            "Completed refresh of {Count} channels: {Queued} total episodes queued",
            results.Count,
            results.Sum(r => r.EpisodesQueued));

        return results;
    }

    /// <summary>
    /// Gets the sync status for a specific channel.
    /// </summary>
    /// <param name="channelId">The channel ID to check.</param>
    /// <returns>True if the channel is currently syncing, false otherwise.</returns>
    public static bool IsChannelSyncing(string channelId)
    {
        return _syncingChannels.TryGetValue(channelId, out var isSyncing) && isSyncing;
    }

    /// <summary>
    /// Gets the IDs of all currently syncing channels.
    /// </summary>
    /// <returns>Dictionary mapping channel IDs to their sync status.</returns>
    public static Dictionary<string, bool> GetSyncingChannels()
    {
        return _syncingChannels.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Creates a new Episode record from yt-dlp video metadata.
    /// </summary>
    private async Task<Episode> CreateEpisodeAsync(
        Channel channel,
        YtDlpMetadata video,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        
        var episode = new Episode
        {
            Id = Guid.NewGuid().ToString("N"),
            VideoId = video.Id,
            ChannelId = channel.Id,
            Title = video.Title,
            Description = video.Description,
            ThumbnailUrl = video.Thumbnail,
            DurationSeconds = video.Duration.HasValue ? (int)video.Duration.Value : null,
            PublishedAt = video.PublishedAt?.DateTime,
            Status = EpisodeStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        return await _episodeRepository.AddAsync(episode, cancellationToken);
    }

    /// <summary>
    /// Downloads channel avatar from YouTube.
    /// </summary>
    private async Task DownloadChannelArtworkAsync(
        Channel channel,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(channel.ThumbnailUrl) && 
            !channel.ThumbnailUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var avatarUrl = await GetChannelAvatarUrlAsync(channel.Url, cancellationToken);

            if (string.IsNullOrEmpty(avatarUrl))
            {
                _logger?.LogDebug("No avatar URL found for channel {ChannelId}", channel.Id);
                return;
            }

            var thumbnailDir = Path.Combine(_downloadDirectory, channel.Id, "thumbnails");
            if (!Directory.Exists(thumbnailDir))
            {
                Directory.CreateDirectory(thumbnailDir);
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            
            var response = await httpClient.GetAsync(avatarUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var extension = GetImageExtension(response.Content.Headers.ContentType?.MediaType);
            var fileName = $"channel_avatar{extension}";
            var thumbnailPath = Path.Combine(thumbnailDir, fileName);

            var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            await File.WriteAllBytesAsync(thumbnailPath, imageBytes, cancellationToken);

            var relativePath = Path.GetRelativePath(_downloadDirectory, thumbnailPath);
            channel.ThumbnailUrl = relativePath;
            _logger?.LogInformation("Downloaded channel avatar for {ChannelId}: {Path}", channel.Id, relativePath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to download channel avatar for {ChannelId}", channel.Id);
        }
    }

    /// <summary>
    /// Gets the channel avatar URL by scraping the channel page.
    /// </summary>
    private static async Task<string?> GetChannelAvatarUrlAsync(string channelUrl, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        var html = await httpClient.GetStringAsync(channelUrl, cancellationToken);

        var patterns = new[]
        {
            @"""avatar"":\s*{[^}]*""thumbnails"":\s*\[\s*{[^}]*""url"":\s*""([^""]+)""",
            @"""thumbnail"":\s*{[^}]*""thumbnails"":\s*\[\s*{[^}]*""url"":\s*""([^""]+)""",
            @"data-thumb=""([^""]+)""",
            @"<img[^>]+class=""[^""]*channel-avatar[^""]*""[^>]+src=""([^""]+)""",
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(html, pattern);
            if (match.Success && !string.IsNullOrEmpty(match.Groups[1].Value))
            {
                return match.Groups[1].Value.Replace(@"\u002F", "/");
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the file extension for an image MIME type.
    /// </summary>
    /// <param name="mediaType">The MIME type (e.g., "image/webp").</param>
    /// <returns>File extension with dot (e.g., ".webp"), or ".jpg" as default.</returns>
    public static string GetImageExtension(string? mediaType)
    {
        return mediaType?.ToLowerInvariant() switch
        {
            "image/webp" => ".webp",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            _ => ".jpg" // Default fallback
        };
    }
}