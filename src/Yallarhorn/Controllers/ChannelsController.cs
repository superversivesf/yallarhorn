using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Models.Api;
using Yallarhorn.Services;

namespace Yallarhorn.Controllers;

/// <summary>
/// API controller for channel management.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class ChannelsController : ControllerBase
{
    private readonly IChannelRepository _channelRepository;
    private readonly IEpisodeRepository _episodeRepository;
    private readonly IFileService _fileService;
    private readonly ILogger<ChannelsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelsController"/> class.
    /// </summary>
    /// <param name="channelRepository">The channel repository.</param>
    /// <param name="episodeRepository">The episode repository.</param>
    /// <param name="fileService">The file service.</param>
    /// <param name="logger">The logger.</param>
    public ChannelsController(
        IChannelRepository channelRepository,
        IEpisodeRepository episodeRepository,
        IFileService fileService,
        ILogger<ChannelsController> logger)
    {
        _channelRepository = channelRepository;
        _episodeRepository = episodeRepository;
        _fileService = fileService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all channels with optional filtering and pagination.
    /// </summary>
    /// <param name="query">Pagination query parameters.</param>
    /// <param name="enabled">Optional filter by enabled status.</param>
    /// <param name="feedType">Optional filter by feed type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of channels.</returns>
    /// <remarks>
    /// Query parameters:
    /// - page: Page number (default: 1)
    /// - limit: Items per page (default: 50, max: 100)
    /// - enabled: Filter by enabled status
    /// - feed_type: Filter by feed type (audio, video, both)
    /// - sort: Sort field (title, created_at, last_refresh_at)
    /// - order: Sort order (asc, desc, default: desc)
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<ChannelResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChannels(
        [FromQuery] PaginationQuery query,
        [FromQuery] bool? enabled = null,
        [FromQuery] string? feedType = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting channels - Page: {Page}, Limit: {Limit}, Enabled: {Enabled}, FeedType: {FeedType}, Sort: {Sort}, Order: {Order}",
            query.Page, query.Limit, enabled, feedType, query.Sort, query.Order);

        // Build filter predicate
        System.Linq.Expressions.Expression<Func<Channel, bool>>? predicate = null;

        if (enabled.HasValue)
        {
            var enabledValue = enabled.Value;
            predicate = c => c.Enabled == enabledValue;
        }

        if (!string.IsNullOrEmpty(feedType) && Enum.TryParse<FeedType>(feedType, true, out var feedTypeEnum))
        {
            if (predicate == null)
            {
                predicate = c => c.FeedType == feedTypeEnum;
            }
            else
            {
                var existingPredicate = predicate;
                predicate = c => existingPredicate.Compile()(c) && c.FeedType == feedTypeEnum;
            }
        }

        // Determine sort expression - use UTC DateTime for SQLite compatibility
        var sortField = query.Sort?.ToLowerInvariant() switch
        {
            "title" => "Title",
            "last_refresh_at" => "LastRefreshAt",
            "created_at" or null => "CreatedAt",
            _ => "CreatedAt"
        };
        var ascending = query.Order?.ToLowerInvariant() == "asc";

        // Get all channels with filter - SQLite doesn't support DateTimeOffset in ORDER BY
        var channels = predicate != null
            ? await _channelRepository.FindAsync(predicate, cancellationToken)
            : await _channelRepository.GetAllAsync(cancellationToken);
        
        // Sort client-side
        var channelList = channels.ToList();
        var sortedChannels = sortField switch
        {
            "Title" => ascending 
                ? channelList.OrderBy(c => c.Title) 
                : channelList.OrderByDescending(c => c.Title),
            "LastRefreshAt" => ascending 
                ? channelList.OrderBy(c => c.LastRefreshAt ?? DateTimeOffset.MinValue) 
                : channelList.OrderByDescending(c => c.LastRefreshAt ?? DateTimeOffset.MinValue),
            _ => ascending 
                ? channelList.OrderBy(c => c.CreatedAt) 
                : channelList.OrderByDescending(c => c.CreatedAt)
        };

        // Paginate
        var totalCount = sortedChannels.Count();
        var pagedChannels = sortedChannels
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .ToList();

        // Build response with episode counts
        var channelResponses = new List<ChannelResponse>();
        foreach (var channel in pagedChannels)
        {
            var episodeCount = await _episodeRepository.CountByChannelIdAsync(channel.Id, cancellationToken);
            channelResponses.Add(await MapToResponseAsync(channel, episodeCount));
        }

        // Build HATEOAS links
        var basePath = BuildBasePath(enabled, feedType);
        var response = BuildPaginatedResponse(channelResponses, totalCount, query.Page, query.Limit, basePath);

        return Ok(response);
    }

    /// <summary>
    /// Gets a single channel by ID.
    /// </summary>
    /// <param name="id">The channel ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The channel details.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ChannelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChannel(string id, CancellationToken cancellationToken = default)
    {
        var channel = await _channelRepository.GetByIdAsync(id, cancellationToken);
        if (channel == null)
        {
            _logger.LogWarning("Channel not found: {ChannelId}", id);
            return NotFound();
        }

        var episodeCount = await _episodeRepository.CountByChannelIdAsync(channel.Id, cancellationToken);
        var response = await MapToResponseAsync(channel, episodeCount);

        return Ok(new { data = response });
    }

    /// <summary>
    /// Creates a new channel to monitor.
    /// </summary>
    /// <param name="request">The create channel request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created channel.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(CreateChannelResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateChannel(
        [FromBody] CreateChannelRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating channel with URL: {Url}", request.Url);

        // Validate URL format
        if (!IsValidYouTubeUrl(request.Url))
        {
            _logger.LogWarning("Invalid YouTube URL: {Url}", request.Url);
            return UnprocessableEntity(new ValidationErrorResponse
            {
                Code = "VALIDATION_ERROR",
                Message = "Invalid YouTube channel URL",
                Field = "url"
            });
        }

        // Check for duplicate URL
        if (await _channelRepository.ExistsByUrlAsync(request.Url, cancellationToken))
        {
            _logger.LogWarning("Channel already exists with URL: {Url}", request.Url);
            return Conflict(new ConflictErrorResponse
            {
                Code = "CONFLICT",
                Message = "Channel already exists",
                Details = $"A channel with URL '{request.Url}' already exists"
            });
        }

        // Parse feed type
        var feedType = FeedType.Audio;
        if (!string.IsNullOrEmpty(request.FeedType))
        {
            feedType = Enum.Parse<FeedType>(request.FeedType, ignoreCase: true);
        }

        // Create channel entity
        var now = DateTimeOffset.UtcNow;
        var channel = new Channel
        {
            Id = GenerateChannelId(),
            Url = request.Url,
            Title = request.Title ?? ExtractChannelTitle(request.Url),
            Description = request.Description,
            EpisodeCountConfig = request.EpisodeCountConfig ?? 50,
            FeedType = feedType,
            Enabled = request.Enabled ?? true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var createdChannel = await _channelRepository.AddAsync(channel, cancellationToken);
        _logger.LogInformation("Created channel: {ChannelId}", createdChannel.Id);

        var episodeCount = await _episodeRepository.CountByChannelIdAsync(createdChannel.Id, cancellationToken);
        var response = await MapToResponseAsync(createdChannel, episodeCount);

        return CreatedAtAction(
            nameof(GetChannel),
            new { id = createdChannel.Id },
            new CreateChannelResponse
            {
                Data = response,
                Message = "Channel created successfully. Initial refresh scheduled."
            });
    }

    /// <summary>
    /// Updates an existing channel's configuration.
    /// </summary>
    /// <param name="id">The channel ID.</param>
    /// <param name="request">The update channel request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated channel.</returns>
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(ChannelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateChannel(
        string id,
        [FromBody] UpdateChannelRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating channel: {ChannelId}", id);

        var channel = await _channelRepository.GetByIdAsync(id, cancellationToken);
        if (channel == null)
        {
            _logger.LogWarning("Channel not found: {ChannelId}", id);
            return NotFound();
        }

        // Apply partial updates
        if (request.Title != null)
        {
            channel.Title = request.Title;
        }

        if (request.Description != null)
        {
            channel.Description = request.Description;
        }

        if (request.EpisodeCountConfig.HasValue)
        {
            channel.EpisodeCountConfig = request.EpisodeCountConfig.Value;
        }

        if (request.FeedType != null)
        {
            channel.FeedType = Enum.Parse<FeedType>(request.FeedType, ignoreCase: true);
        }

        if (request.Enabled.HasValue)
        {
            channel.Enabled = request.Enabled.Value;
        }

        // Update the timestamp
        channel.UpdatedAt = DateTimeOffset.UtcNow;

        await _channelRepository.UpdateAsync(channel, cancellationToken);
        _logger.LogInformation("Updated channel: {ChannelId}", id);

        var episodeCount = await _episodeRepository.CountByChannelIdAsync(channel.Id, cancellationToken);
        var response = await MapToResponseAsync(channel, episodeCount);

        return Ok(new { data = response });
    }

    /// <summary>
    /// Deletes a channel and all its episodes.
    /// </summary>
    /// <param name="id">The channel ID.</param>
    /// <param name="delete_files">Whether to delete media files from disk (default: true).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deletion result.</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(DeleteChannelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteChannel(
        string id,
        [FromQuery(Name = "delete_files")] bool delete_files = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting channel: {ChannelId}, delete_files: {DeleteFiles}", id, delete_files);

        var channel = await _channelRepository.GetByIdAsync(id, cancellationToken);
        if (channel == null)
        {
            _logger.LogWarning("Channel not found: {ChannelId}", id);
            return NotFound();
        }

        // Get all episodes for the channel
        var episodes = await _episodeRepository.GetByChannelIdAsync(id, cancellationToken: cancellationToken);
        var episodeList = episodes.ToList();

        var result = new DeleteChannelResponse
        {
            ChannelId = id,
            EpisodesDeleted = episodeList.Count,
            FilesDeleted = 0,
            BytesFreed = 0
        };

        // Delete files if requested
        if (delete_files)
        {
            foreach (var episode in episodeList)
            {
                var (filesDeleted, bytesFreed) = DeleteEpisodeFiles(episode);
                result.FilesDeleted += filesDeleted;
                result.BytesFreed += bytesFreed;
            }
        }

        // Delete all episodes from database
        if (episodeList.Count > 0)
        {
            await _episodeRepository.DeleteRangeAsync(episodeList, cancellationToken);
            _logger.LogInformation("Deleted {Count} episodes for channel {ChannelId}", episodeList.Count, id);
        }

        // Delete the channel
        await _channelRepository.DeleteAsync(channel, cancellationToken);
        _logger.LogInformation(
            "Deleted channel {ChannelId}: {EpisodesDeleted} episodes, {FilesDeleted} files, {BytesFreed} bytes freed",
            id, result.EpisodesDeleted, result.FilesDeleted, result.BytesFreed);

        return Ok(result);
    }

    /// <summary>
    /// Deletes media files associated with an episode.
    /// </summary>
    /// <param name="episode">The episode to delete files for.</param>
    /// <returns>Tuple of (files deleted, bytes freed).</returns>
    private (int filesDeleted, long bytesFreed) DeleteEpisodeFiles(Episode episode)
    {
        var filesDeleted = 0;
        var bytesFreed = 0L;

        // Delete audio file
        if (!string.IsNullOrEmpty(episode.FilePathAudio))
        {
            try
            {
                if (_fileService.FileExists(episode.FilePathAudio))
                {
                    bytesFreed += episode.FileSizeAudio ?? 0;
                    _fileService.DeleteFile(episode.FilePathAudio);
                    filesDeleted++;
                    _logger.LogDebug("Deleted audio file: {Path}", episode.FilePathAudio);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete audio file: {Path}", episode.FilePathAudio);
            }
        }

        // Delete video file
        if (!string.IsNullOrEmpty(episode.FilePathVideo))
        {
            try
            {
                if (_fileService.FileExists(episode.FilePathVideo))
                {
                    bytesFreed += episode.FileSizeVideo ?? 0;
                    _fileService.DeleteFile(episode.FilePathVideo);
                    filesDeleted++;
                    _logger.LogDebug("Deleted video file: {Path}", episode.FilePathVideo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete video file: {Path}", episode.FilePathVideo);
            }
        }

        return (filesDeleted, bytesFreed);
    }

    /// <summary>
    /// Validates if a URL is a valid YouTube channel URL.
    /// </summary>
    private static bool IsValidYouTubeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        try
        {
            var uri = new Uri(url);
            
            // Check if it's a YouTube domain
            if (!uri.Host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) &&
                !uri.Host.Equals("www.youtube.com", StringComparison.OrdinalIgnoreCase) &&
                !uri.Host.Equals("m.youtube.com", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Valid channel URL patterns:
            // - https://youtube.com/@channelname
            // - https://youtube.com/c/channelname
            // - https://youtube.com/channel/UCxxxxxxxxxxxxxxxxxxxxxx
            // - https://youtube.com/user/username
            var path = uri.AbsolutePath.Trim('/');

            return path.StartsWith("@", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("c/", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("channel/", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("user/", StringComparison.OrdinalIgnoreCase);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts a channel title placeholder from the URL.
    /// </summary>
    private static string ExtractChannelTitle(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath.Trim('/');

            // Handle @channelname
            if (path.StartsWith("@"))
            {
                return path;
            }

            // Handle channel/ID or user/username
            var parts = path.Split('/');
            if (parts.Length >= 2)
            {
                return parts[^1];
            }

            return "Unknown Channel";
        }
        catch
        {
            return "Unknown Channel";
        }
    }

    /// <summary>
    /// Generates a unique channel ID.
    /// </summary>
    private static string GenerateChannelId()
    {
        return $"ch-{Guid.NewGuid():N}".ToLowerInvariant();
    }

    /// <summary>
    /// Maps a channel entity to a response model.
    /// </summary>
    private Task<ChannelResponse> MapToResponseAsync(Channel channel, int episodeCount)
    {
        var response = new ChannelResponse
        {
            Id = channel.Id,
            Url = channel.Url,
            Title = channel.Title,
            Description = channel.Description,
            ThumbnailUrl = channel.ThumbnailUrl,
            EpisodeCountConfig = channel.EpisodeCountConfig,
            FeedType = channel.FeedType.ToString().ToLowerInvariant(),
            Enabled = channel.Enabled,
            EpisodeCount = episodeCount,
            LastRefreshAt = channel.LastRefreshAt,
            CreatedAt = channel.CreatedAt,
            UpdatedAt = channel.UpdatedAt,
            Links = BuildChannelLinks(channel.Id)
        };

        return Task.FromResult(response);
    }

    /// <summary>
    /// Builds HATEOAS links for a channel.
    /// </summary>
    private static Dictionary<string, Link> BuildChannelLinks(string channelId)
    {
        return new Dictionary<string, Link>
        {
            ["self"] = new Link { Href = $"/api/v1/channels/{channelId}", Rel = "self" },
            ["episodes"] = new Link { Href = $"/api/v1/channels/{channelId}/episodes", Rel = "episodes" },
            ["refresh"] = new Link { Href = $"/api/v1/channels/{channelId}/refresh", Rel = "refresh" }
        };
    }

    /// <summary>
    /// Gets the sort parameters from the query.
    /// </summary>
    private (System.Linq.Expressions.Expression<Func<Channel, object>>? orderBy, bool ascending) GetSortParameters(PaginationQuery query)
    {
        var ascending = query.Order?.ToLowerInvariant() == "asc";
        
        var orderBy = query.Sort?.ToLowerInvariant() switch
        {
            "title" => (System.Linq.Expressions.Expression<Func<Channel, object>>)(c => c.Title),
            "last_refresh_at" => c => c.LastRefreshAt!,
            "created_at" or null => c => c.CreatedAt,
            _ => c => c.CreatedAt
        };

        return (orderBy, ascending);
    }

    /// <summary>
    /// Builds the base path for pagination links, preserving filters.
    /// </summary>
    private string BuildBasePath(bool? enabled, string? feedType)
    {
        var queryParams = new List<string>();

        if (enabled.HasValue)
        {
            queryParams.Add($"enabled={enabled.Value.ToString().ToLowerInvariant()}");
        }

        if (!string.IsNullOrEmpty(feedType))
        {
            queryParams.Add($"feed_type={feedType.ToLowerInvariant()}");
        }

        var queryString = queryParams.Count > 0 ? $"?{string.Join("&", queryParams)}" : "";
        return $"/api/v1/channels{queryString}";
    }

    /// <summary>
    /// Builds a paginated response with HATEOAS links.
    /// </summary>
    private PaginatedResponse<ChannelResponse> BuildPaginatedResponse(
        List<ChannelResponse> data,
        int totalCount,
        int page,
        int limit,
        string basePath)
    {
        return PaginatedResponse<ChannelResponse>.Create(data, page, limit, totalCount, basePath);
    }
}