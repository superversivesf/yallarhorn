using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Models.Api;
using Yallarhorn.Services;

namespace Yallarhorn.Controllers;

/// <summary>
/// API controller for manual trigger operations.
/// </summary>
[ApiController]
[Route("api/v1")]
public class TriggersController : ControllerBase
{
    private readonly IChannelRepository _channelRepository;
    private readonly IEpisodeRepository _episodeRepository;
    private readonly IChannelRefreshService _channelRefreshService;
    private readonly IDownloadQueueService _downloadQueueService;
    private readonly IDownloadQueueRepository _downloadQueueRepository;
    private readonly ILogger<TriggersController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggersController"/> class.
    /// </summary>
    /// <param name="channelRepository">The channel repository.</param>
    /// <param name="episodeRepository">The episode repository.</param>
    /// <param name="channelRefreshService">The channel refresh service.</param>
    /// <param name="downloadQueueService">The download queue service.</param>
    /// <param name="downloadQueueRepository">The download queue repository.</param>
    /// <param name="logger">The logger.</param>
    public TriggersController(
        IChannelRepository channelRepository,
        IEpisodeRepository episodeRepository,
        IChannelRefreshService channelRefreshService,
        IDownloadQueueService downloadQueueService,
        IDownloadQueueRepository downloadQueueRepository,
        ILogger<TriggersController> logger)
    {
        _channelRepository = channelRepository;
        _episodeRepository = episodeRepository;
        _channelRefreshService = channelRefreshService;
        _downloadQueueService = downloadQueueService;
        _downloadQueueRepository = downloadQueueRepository;
        _logger = logger;
    }

    /// <summary>
    /// Triggers a refresh for a single channel.
    /// </summary>
    /// <param name="id">The channel ID.</param>
    /// <param name="request">Optional refresh options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>202 Accepted with refresh status.</returns>
    /// <remarks>
    /// POST /api/v1/channels/{id}/refresh
    /// 
    /// Triggers an immediate refresh for the specified channel to discover new episodes.
    /// 
    /// Request body (optional):
    /// - force: When true, refresh even if recently refreshed (default: false)
    /// 
    /// Status codes:
    /// - 202: Refresh queued successfully
    /// - 404: Channel not found
    /// </remarks>
    [HttpPost("channels/{id}/refresh")]
    [ProducesResponseType(typeof(RefreshChannelResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RefreshChannel(
        string id,
        [FromBody] RefreshChannelRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual refresh triggered for channel {ChannelId}, force: {Force}", id, request?.Force ?? false);

        // Verify channel exists
        var channel = await _channelRepository.GetByIdAsync(id, cancellationToken);
        if (channel == null)
        {
            _logger.LogWarning("Channel {ChannelId} not found for manual refresh", id);
            return NotFound(new
            {
                error = new
                {
                    code = "NOT_FOUND",
                    message = "Channel not found",
                    details = $"No channel exists with ID '{id}'"
                }
            });
        }

        // Trigger the refresh
        var result = await _channelRefreshService.RefreshChannelAsync(id, cancellationToken);

        _logger.LogInformation(
            "Refresh completed for channel {ChannelId}: {VideosFound} videos found, {EpisodesQueued} episodes queued",
            id, result.VideosFound, result.EpisodesQueued);

        return Accepted(new RefreshChannelResponse
        {
            Message = "Refresh queued",
            ChannelId = id
        });
    }

    /// <summary>
    /// Triggers a refresh for all enabled channels.
    /// </summary>
    /// <param name="request">Optional refresh options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>202 Accepted with refresh status.</returns>
    /// <remarks>
    /// POST /api/v1/refresh-all
    /// 
    /// Triggers a refresh for all enabled channels.
    /// 
    /// Request body (optional):
    /// - force: When true, refresh all channels even if recently refreshed (default: false)
    /// 
    /// Status codes:
    /// - 202: Refresh all queued successfully
    /// </remarks>
    [HttpPost("refresh-all")]
    [ProducesResponseType(typeof(RefreshAllChannelsResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RefreshAllChannels(
        [FromBody] RefreshChannelRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual refresh-all triggered, force: {Force}", request?.Force ?? false);

        // Trigger refresh for all enabled channels
        var results = await _channelRefreshService.RefreshAllChannelsAsync(cancellationToken);
        var resultsList = results.ToList();

        var channelsRefreshed = resultsList.Count;
        var totalEpisodesQueued = resultsList.Sum(r => r.EpisodesQueued);

        _logger.LogInformation(
            "Refresh-all completed: {ChannelsRefreshed} channels refreshed, {TotalEpisodesQueued} episodes queued",
            channelsRefreshed, totalEpisodesQueued);

        return Accepted(new RefreshAllChannelsResponse
        {
            Message = "Refresh all queued",
            ChannelsRefreshed = channelsRefreshed
        });
    }

    /// <summary>
    /// Retries a failed episode download.
    /// </summary>
    /// <param name="id">The episode ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>202 Accepted or 400/404 error.</returns>
    /// <remarks>
    /// POST /api/v1/episodes/{id}/retry
    /// 
    /// Resets a failed episode's attempts and requeues it for download.
    /// Only works for episodes in Failed or Retrying status.
    /// 
    /// Status codes:
    /// - 202: Retry queued successfully
    /// - 400: Episode is not in a retryable state
    /// - 404: Episode not found
    /// </remarks>
    [HttpPost("episodes/{id}/retry")]
    [ProducesResponseType(typeof(RetryEpisodeResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryEpisode(
        string id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manual retry triggered for episode {EpisodeId}", id);

        // Verify episode exists
        var episode = await _episodeRepository.GetByIdAsync(id, cancellationToken);
        if (episode == null)
        {
            _logger.LogWarning("Episode {EpisodeId} not found for manual retry", id);
            return NotFound(new
            {
                error = new
                {
                    code = "NOT_FOUND",
                    message = "Episode not found",
                    details = $"No episode exists with ID '{id}'"
                }
            });
        }

        // Check if episode is in a retryable state
        if (episode.Status != EpisodeStatus.Failed)
        {
            _logger.LogWarning(
                "Episode {EpisodeId} cannot be retried: status is {Status}",
                id, episode.Status);

            return BadRequest(new
            {
                error = new
                {
                    code = "INVALID_STATUS",
                    message = "Episode is not in a retryable state",
                    details = $"Episode status is '{episode.Status.ToString().ToLowerInvariant()}', but must be 'failed' to retry"
                }
            });
        }

        // Reset episode state for retry
        episode.Status = EpisodeStatus.Pending;
        episode.RetryCount = 0;
        episode.ErrorMessage = null;
        episode.UpdatedAt = DateTimeOffset.UtcNow;

        await _episodeRepository.UpdateAsync(episode, cancellationToken);

        // Get existing queue item and remove it if exists, then enqueue fresh
        var existingQueueItem = await _downloadQueueRepository.GetByEpisodeIdAsync(id, cancellationToken);
        if (existingQueueItem != null)
        {
            // The existing queue item will be replaced by the new enqueue
            _logger.LogDebug(
                "Episode {EpisodeId} has existing queue item {QueueId} with status {Status}",
                id, existingQueueItem.Id, existingQueueItem.Status);
        }

        // Enqueue for download
        try
        {
            await _downloadQueueService.EnqueueAsync(id, priority: 5, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            // Episode already queued - this is fine for retry
            _logger.LogInformation(
                "Episode {EpisodeId} already in queue: {Message}",
                id, ex.Message);
        }

        _logger.LogInformation(
            "Episode {EpisodeId} reset and queued for retry",
            id);

        return Accepted(new RetryEpisodeResponse
        {
            Message = "Retry queued",
            EpisodeId = id,
            Status = "pending"
        });
    }
}