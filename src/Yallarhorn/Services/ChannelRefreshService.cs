namespace Yallarhorn.Services;

using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelRefreshService"/> class.
    /// </summary>
    /// <param name="channelRepository">The channel repository.</param>
    /// <param name="episodeRepository">The episode repository.</param>
    /// <param name="ytDlpClient">The yt-dlp client.</param>
    /// <param name="queueService">The download queue service.</param>
    /// <param name="logger">Optional logger.</param>
    public ChannelRefreshService(
        IChannelRepository channelRepository,
        IEpisodeRepository episodeRepository,
        IYtDlpClient ytDlpClient,
        IDownloadQueueService queueService,
        ILogger<ChannelRefreshService>? logger = null)
    {
        _channelRepository = channelRepository;
        _episodeRepository = episodeRepository;
        _ytDlpClient = ytDlpClient;
        _queueService = queueService;
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

            // Step 5: Update channel's last_refresh_at timestamp
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
            DurationSeconds = video.Duration,
            PublishedAt = video.PublishedAt?.DateTime,
            Status = EpisodeStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        return await _episodeRepository.AddAsync(episode, cancellationToken);
    }
}