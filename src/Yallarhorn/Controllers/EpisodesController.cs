using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Models.Api;
using Yallarhorn.Services;

namespace Yallarhorn.Controllers;

/// <summary>
/// API controller for episode management.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class EpisodesController : ControllerBase
{
    private readonly IChannelRepository _channelRepository;
    private readonly IEpisodeRepository _episodeRepository;
    private readonly IDownloadQueueRepository _downloadQueueRepository;
    private readonly IFileService _fileService;
    private readonly ILogger<EpisodesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodesController"/> class.
    /// </summary>
    /// <param name="channelRepository">The channel repository.</param>
    /// <param name="episodeRepository">The episode repository.</param>
    /// <param name="downloadQueueRepository">The download queue repository.</param>
    /// <param name="fileService">The file service.</param>
    /// <param name="logger">The logger.</param>
    public EpisodesController(
        IChannelRepository channelRepository,
        IEpisodeRepository episodeRepository,
        IDownloadQueueRepository downloadQueueRepository,
        IFileService fileService,
        ILogger<EpisodesController> logger)
    {
        _channelRepository = channelRepository;
        _episodeRepository = episodeRepository;
        _downloadQueueRepository = downloadQueueRepository;
        _fileService = fileService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all episodes for a specific channel with optional filtering and pagination.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="query">Pagination query parameters.</param>
    /// <param name="status">Optional filter by status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of episodes.</returns>
    /// <remarks>
    /// Query parameters:
    /// - page: Page number (default: 1)
    /// - limit: Items per page (default: 50, max: 100)
    /// - status: Filter by status (pending, downloading, processing, completed, failed, deleted)
    /// - sort: Sort field (title, published_at, downloaded_at, duration_seconds)
    /// - order: Sort order (asc, desc, default: desc)
    /// </remarks>
    [HttpGet("/api/v1/channels/{channelId}/episodes")]
    [ProducesResponseType(typeof(PaginatedResponse<EpisodeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEpisodesByChannel(
        string channelId,
        [FromQuery] PaginationQuery query,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting episodes for channel {ChannelId} - Page: {Page}, Limit: {Limit}, Status: {Status}, Sort: {Sort}, Order: {Order}",
            channelId, query.Page, query.Limit, status, query.Sort, query.Order);

        // Verify channel exists
        var channel = await _channelRepository.GetByIdAsync(channelId, cancellationToken);
        if (channel == null)
        {
            _logger.LogWarning("Channel not found: {ChannelId}", channelId);
            return NotFound();
        }

        // Build filter predicate
        System.Linq.Expressions.Expression<Func<Episode, bool>>? predicate = null;
        predicate = e => e.ChannelId == channelId;

        // Apply status filter if provided
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<EpisodeStatus>(status, true, out var statusEnum))
        {
            var capturedPredicate = predicate;
            predicate = e => capturedPredicate.Compile()(e) && e.Status == statusEnum;
        }

        // Determine sort expression - SQLite doesn't support DateTimeOffset in ORDER BY
        var sortField = query.Sort?.ToLowerInvariant() switch
        {
            "title" => "Title",
            "downloaded_at" => "DownloadedAt",
            "duration_seconds" => "DurationSeconds",
            "published_at" or null => "PublishedAt",
            _ => "PublishedAt"
        };
        var ascending = query.Order?.ToLowerInvariant() == "asc";

        // Get episodes with filter - use FindAsync for predicate support
        var episodes = await _episodeRepository.FindAsync(predicate, cancellationToken);
        
        // Sort client-side
        var episodeList = episodes.ToList();
        var sortedEpisodes = sortField switch
        {
            "Title" => ascending 
                ? episodeList.OrderBy(e => e.Title) 
                : episodeList.OrderByDescending(e => e.Title),
            "DownloadedAt" => ascending 
                ? episodeList.OrderBy(e => e.DownloadedAt ?? DateTimeOffset.MinValue) 
                : episodeList.OrderByDescending(e => e.DownloadedAt ?? DateTimeOffset.MinValue),
            "DurationSeconds" => ascending 
                ? episodeList.OrderBy(e => e.DurationSeconds ?? 0) 
                : episodeList.OrderByDescending(e => e.DurationSeconds ?? 0),
            _ => ascending 
                ? episodeList.OrderBy(e => e.PublishedAt ?? DateTimeOffset.MinValue) 
                : episodeList.OrderByDescending(e => e.PublishedAt ?? DateTimeOffset.MinValue)
        };

        // Paginate
        var totalCount = sortedEpisodes.Count();
        var pagedEpisodes = sortedEpisodes
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .ToList();

        // Map to response
        var episodeResponses = pagedEpisodes.Select(MapToResponse).ToList();

        // Build HATEOAS links for pagination
        var basePath = BuildBasePath(channelId, status);
        var response = BuildPaginatedResponse(episodeResponses, totalCount, query.Page, query.Limit, basePath, channelId);

        return Ok(response);
    }

    /// <summary>
    /// Gets a single episode by ID.
    /// </summary>
    /// <param name="id">The episode ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The episode details or 404 if not found.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EpisodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEpisode(
        string id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting episode {EpisodeId}", id);

        var episode = await _episodeRepository.GetByIdAsync(id, cancellationToken);
        if (episode == null)
        {
            _logger.LogWarning("Episode not found: {EpisodeId}", id);
            return NotFound();
        }

        var response = MapToResponse(episode);
        return Ok(response);
    }

    /// <summary>
    /// Deletes an episode and optionally its associated files.
    /// </summary>
    /// <param name="id">The episode ID.</param>
    /// <param name="delete_files">Whether to delete files from disk (default: true).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the deletion operation.</returns>
    /// <remarks>
    /// DELETE /api/v1/episodes/{id}
    /// Query parameters:
    /// - delete_files: Whether to delete files from disk (default: true)
    /// 
    /// Status codes:
    /// - 200: Episode deleted successfully
    /// - 404: Episode not found
    /// - 409: Episode is currently downloading (conflict)
    /// </remarks>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(DeleteEpisodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEpisode(
        string id,
        [FromQuery(Name = "delete_files")] bool delete_files = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting episode {EpisodeId}, delete_files: {DeleteFiles}", id, delete_files);

        var episode = await _episodeRepository.GetByIdAsync(id, cancellationToken);
        if (episode == null)
        {
            _logger.LogWarning("Episode not found: {EpisodeId}", id);
            return NotFound();
        }

        // Check if episode is currently downloading - return 409 Conflict
        if (episode.Status == EpisodeStatus.Downloading)
        {
            _logger.LogWarning(
                "Cannot delete episode {EpisodeId}: currently downloading", id);
            return Conflict(new
            {
                error = new
                {
                    code = "CONFLICT",
                    message = "Episode is currently downloading",
                    details = "Cannot delete an episode while it is being downloaded. Wait for the download to complete or cancel it first."
                }
            });
        }

        // Remove from download queue if status is pending
        if (episode.Status == EpisodeStatus.Pending)
        {
            var queueItem = await _downloadQueueRepository.GetByEpisodeIdAsync(id, cancellationToken);
            if (queueItem != null)
            {
                _logger.LogDebug(
                    "Removing episode {EpisodeId} from download queue (queue id: {QueueId})",
                    id, queueItem.Id);
                await _downloadQueueRepository.DeleteAsync(queueItem, cancellationToken);
            }
        }

        // Track deletion statistics
        int filesDeleted = 0;
        long bytesFreed = 0;

        // Delete files from disk if requested
        if (delete_files)
        {
            // Delete audio file
            if (!string.IsNullOrEmpty(episode.FilePathAudio))
            {
                if (_fileService.FileExists(episode.FilePathAudio))
                {
                    _fileService.DeleteFile(episode.FilePathAudio);
                    filesDeleted++;
                    bytesFreed += episode.FileSizeAudio ?? 0;
                    _logger.LogDebug("Deleted audio file: {FilePath}", episode.FilePathAudio);
                }
            }

            // Delete video file
            if (!string.IsNullOrEmpty(episode.FilePathVideo))
            {
                if (_fileService.FileExists(episode.FilePathVideo))
                {
                    _fileService.DeleteFile(episode.FilePathVideo);
                    filesDeleted++;
                    bytesFreed += episode.FileSizeVideo ?? 0;
                    _logger.LogDebug("Deleted video file: {FilePath}", episode.FilePathVideo);
                }
            }
        }

        // Delete episode from database
        await _episodeRepository.DeleteAsync(episode, cancellationToken);

        _logger.LogInformation(
            "Deleted episode {EpisodeId}: {FilesDeleted} files deleted, {BytesFreed} bytes freed",
            id, filesDeleted, bytesFreed);

        var result = new DeleteEpisodeResponse
        {
            EpisodeId = id,
            FilesDeleted = filesDeleted,
            BytesFreed = bytesFreed
        };

        return Ok(new
        {
            message = "Episode deleted successfully",
            deleted = result
        });
    }

    /// <summary>
    /// Maps an episode entity to a response model.
    /// </summary>
    private static EpisodeResponse MapToResponse(Episode episode)
    {
        var response = new EpisodeResponse
        {
            Id = episode.Id,
            VideoId = episode.VideoId,
            ChannelId = episode.ChannelId,
            Title = episode.Title,
            Description = episode.Description,
            ThumbnailUrl = episode.ThumbnailUrl,
            DurationSeconds = episode.DurationSeconds,
            PublishedAt = episode.PublishedAt,
            DownloadedAt = episode.DownloadedAt,
            FilePathAudio = episode.FilePathAudio,
            FilePathVideo = episode.FilePathVideo,
            FileSizeAudio = episode.FileSizeAudio,
            FileSizeVideo = episode.FileSizeVideo,
            Status = episode.Status.ToString().ToLowerInvariant(),
            RetryCount = episode.RetryCount,
            ErrorMessage = episode.ErrorMessage,
            CreatedAt = episode.CreatedAt,
            UpdatedAt = episode.UpdatedAt,
            Links = BuildEpisodeLinks(episode)
        };

        return response;
    }

    /// <summary>
    /// Builds HATEOAS links for an episode.
    /// </summary>
    private static Dictionary<string, Link> BuildEpisodeLinks(Episode episode)
    {
        var links = new Dictionary<string, Link>
        {
            ["self"] = new Link { Href = $"/api/v1/episodes/{episode.Id}", Rel = "self" },
            ["channel"] = new Link { Href = $"/api/v1/channels/{episode.ChannelId}", Rel = "channel" }
        };

        // Add audio file link if exists
        if (!string.IsNullOrEmpty(episode.FilePathAudio))
        {
            links["audio_file"] = new Link { Href = $"/feeds/{episode.FilePathAudio}", Rel = "audio_file" };
        }

        // Add video file link if exists
        if (!string.IsNullOrEmpty(episode.FilePathVideo))
        {
            links["video_file"] = new Link { Href = $"/feeds/{episode.FilePathVideo}", Rel = "video_file" };
        }

        return links;
    }

    /// <summary>
    /// Gets the sort parameters from the query.
    /// </summary>
    private (System.Linq.Expressions.Expression<Func<Episode, object>>? orderBy, bool ascending) GetSortParameters(PaginationQuery query)
    {
        var ascending = query.Order?.ToLowerInvariant() == "asc";

        var orderBy = query.Sort?.ToLowerInvariant() switch
        {
            "title" => (System.Linq.Expressions.Expression<Func<Episode, object>>)(e => e.Title),
            "downloaded_at" => e => e.DownloadedAt!,
            "duration_seconds" => e => (object?)e.DurationSeconds ?? 0,
            "published_at" or null => (System.Linq.Expressions.Expression<Func<Episode, object>>)(e => e.PublishedAt!),
            _ => e => e.PublishedAt!
        };

        return (orderBy, ascending);
    }

    /// <summary>
    /// Builds the base path for pagination links, preserving filters.
    /// </summary>
    private static string BuildBasePath(string channelId, string? status)
    {
        var queryParams = new List<string>();

        if (!string.IsNullOrEmpty(status))
        {
            queryParams.Add($"status={status.ToLowerInvariant()}");
        }

        var queryString = queryParams.Count > 0 ? $"?{string.Join("&", queryParams)}" : "";
        return $"/api/v1/channels/{channelId}/episodes{queryString}";
    }

    /// <summary>
    /// Builds a paginated response with HATEOAS links.
    /// </summary>
    private static PaginatedResponse<EpisodeResponse> BuildPaginatedResponse(
        List<EpisodeResponse> data,
        int totalCount,
        int page,
        int limit,
        string basePath,
        string channelId)
    {
        var totalPages = (int)Math.Ceiling(totalCount / (double)limit);
        var links = GenerateLinks(basePath, page, limit, totalPages, channelId);

        return new PaginatedResponse<EpisodeResponse>
        {
            Data = data,
            Page = page,
            Limit = limit,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Links = links
        };
    }

    /// <summary>
    /// Generates HATEOAS links for pagination navigation.
    /// </summary>
    private static Dictionary<string, Link> GenerateLinks(
        string basePath,
        int currentPage,
        int limit,
        int totalPages,
        string channelId)
    {
        var links = new Dictionary<string, Link>();

        // Parse base path to preserve existing query parameters
        var (path, existingQuery) = ParseBasePath(basePath);

        // Self link (current page)
        links["self"] = new Link
        {
            Href = BuildUrl(path, existingQuery, currentPage, limit),
            Rel = "self"
        };

        // Channel link
        links["channel"] = new Link
        {
            Href = $"/api/v1/channels/{channelId}",
            Rel = "channel"
        };

        // First page link
        links["first"] = new Link
        {
            Href = BuildUrl(path, existingQuery, 1, limit),
            Rel = "first"
        };

        // Last page link
        if (totalPages > 0)
        {
            links["last"] = new Link
            {
                Href = BuildUrl(path, existingQuery, totalPages, limit),
                Rel = "last"
            };
        }

        // Next page link (only if not on last page)
        if (currentPage < totalPages)
        {
            links["next"] = new Link
            {
                Href = BuildUrl(path, existingQuery, currentPage + 1, limit),
                Rel = "next"
            };
        }

        // Previous page link (only if not on first page)
        if (currentPage > 1)
        {
            links["prev"] = new Link
            {
                Href = BuildUrl(path, existingQuery, currentPage - 1, limit),
                Rel = "prev"
            };
        }

        return links;
    }

    /// <summary>
    /// Parses a base path into path and query components.
    /// </summary>
    private static (string path, string query) ParseBasePath(string basePath)
    {
        var separatorIndex = basePath.IndexOf('?');
        if (separatorIndex < 0)
        {
            return (basePath, string.Empty);
        }

        return (basePath[..separatorIndex], basePath[(separatorIndex + 1)..]);
    }

    /// <summary>
    /// Builds a URL with pagination query parameters.
    /// </summary>
    private static string BuildUrl(string path, string existingQuery, int page, int limit)
    {
        var queryParts = new List<string> { $"page={page}", $"limit={limit}" };

        // Preserve existing query parameters (but override page/limit if present)
        if (!string.IsNullOrEmpty(existingQuery))
        {
            foreach (var part in existingQuery.Split('&'))
            {
                if (!part.StartsWith("page=") && !part.StartsWith("limit="))
                {
                    queryParts.Add(part);
                }
            }
        }

        return $"{path}?{string.Join("&", queryParts)}";
    }
}