namespace Yallarhorn.Services;

using Microsoft.Extensions.Logging;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;

/// <summary>
/// Result of a cleanup operation for a single channel.
/// </summary>
public class CleanupResult
{
    /// <summary>
    /// Gets or sets the channel ID that was cleaned.
    /// </summary>
    public required string ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the number of episodes removed.
    /// </summary>
    public int EpisodesRemoved { get; set; }

    /// <summary>
    /// Gets or sets the total bytes freed.
    /// </summary>
    public long BytesFreed { get; set; }
}

/// <summary>
/// Interface for file operations (for testability).
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Deletes a file if it exists.
    /// </summary>
    /// <param name="path">The full path to the file.</param>
    void DeleteFile(string path);

    /// <summary>
    /// Checks if a file exists.
    /// </summary>
    /// <param name="path">The full path to the file.</param>
    /// <returns>True if the file exists.</returns>
    bool FileExists(string path);

    /// <summary>
    /// Gets the file stream for reading.
    /// </summary>
    /// <param name="path">The full path to the file.</param>
    /// <returns>The file stream, or null if not found.</returns>
    Stream? GetFileStream(string path);

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    /// <param name="path">The full path to the file.</param>
    /// <returns>The file size, or 0 if not found.</returns>
    long GetFileSize(string path);
}

/// <summary>
/// Default implementation of file service using System.IO.
/// </summary>
public class FileService : IFileService
{
    /// <inheritdoc />
    public void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <inheritdoc />
    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    /// <inheritdoc />
    public Stream? GetFileStream(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        return File.OpenRead(path);
    }

    /// <inheritdoc />
    public long GetFileSize(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? info.Length : 0;
    }
}

/// <summary>
/// Interface for episode cleanup service operations.
/// </summary>
public interface IEpisodeCleanupService
{
    /// <summary>
    /// Cleans up episodes outside the rolling window for a specific channel.
    /// Removes episodes NOT in top N by published_at DESC.
    /// </summary>
    /// <param name="channelId">The channel ID to clean up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cleanup result.</returns>
    Task<CleanupResult> CleanupChannelAsync(string channelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up episodes outside the rolling window for all enabled channels.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of cleanup results per channel.</returns>
    Task<IEnumerable<CleanupResult>> CleanupAllChannelsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for removing episodes outside the rolling window.
/// Handles file deletion and storage tracking.
/// </summary>
public class EpisodeCleanupService : IEpisodeCleanupService
{
    private readonly IEpisodeRepository _episodeRepository;
    private readonly IChannelRepository _channelRepository;
    private readonly IFileService _fileService;
    private readonly string _downloadDir;
    private readonly ILogger<EpisodeCleanupService>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeCleanupService"/> class.
    /// </summary>
    /// <param name="episodeRepository">The episode repository.</param>
    /// <param name="channelRepository">The channel repository.</param>
    /// <param name="fileService">The file service.</param>
    /// <param name="downloadDir">The download directory path.</param>
    /// <param name="logger">Optional logger.</param>
    public EpisodeCleanupService(
        IEpisodeRepository episodeRepository,
        IChannelRepository channelRepository,
        IFileService fileService,
        string downloadDir,
        ILogger<EpisodeCleanupService>? logger = null)
    {
        _episodeRepository = episodeRepository;
        _channelRepository = channelRepository;
        _fileService = fileService;
        _downloadDir = downloadDir;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CleanupResult> CleanupChannelAsync(string channelId, CancellationToken cancellationToken = default)
    {
        var result = new CleanupResult
        {
            ChannelId = channelId,
            EpisodesRemoved = 0,
            BytesFreed = 0
        };

        var channel = await _channelRepository.GetByIdAsync(channelId, cancellationToken);
        if (channel == null)
        {
            _logger?.LogWarning("Channel {ChannelId} not found for cleanup", channelId);
            return result;
        }

        _logger?.LogInformation(
            "Starting cleanup for channel {ChannelId} with episode count config {EpisodeCountConfig}",
            channelId, channel.EpisodeCountConfig);

        // Get all completed episodes for the channel, ordered by published_at DESC
        var completedEpisodes = await _episodeRepository.GetByChannelIdAsync(channelId, cancellationToken: cancellationToken);
        var completedList = completedEpisodes
            .Where(e => e.Status == EpisodeStatus.Completed)
            .OrderByDescending(e => e.PublishedAt ?? DateTimeOffset.MinValue)
            .ToList();

        _logger?.LogDebug(
            "Found {Count} completed episodes for channel {ChannelId}",
            completedList.Count, channelId);

        // Identify episodes outside the rolling window (episodes after the first N)
        var episodesToDelete = completedList
            .Skip(channel.EpisodeCountConfig)
            .ToList();

        if (episodesToDelete.Count == 0)
        {
            _logger?.LogInformation(
                "No episodes to clean for channel {ChannelId} (has {Count} episodes, keeps {Config})",
                channelId, completedList.Count, channel.EpisodeCountConfig);
            return result;
        }

        _logger?.LogInformation(
            "Cleaning up {Count} episodes for channel {ChannelId}",
            episodesToDelete.Count, channelId);

        foreach (var episode in episodesToDelete)
        {
            try
            {
                // Calculate bytes to free before deletion
                var bytesToFree = (episode.FileSizeAudio ?? 0) + (episode.FileSizeVideo ?? 0);

                // Delete files from disk
                await DeleteEpisodeFilesAsync(episode);

                // Mark episode as deleted
                episode.Status = EpisodeStatus.Deleted;
                episode.UpdatedAt = DateTimeOffset.UtcNow;
                await _episodeRepository.UpdateAsync(episode, cancellationToken);

                result.EpisodesRemoved++;
                result.BytesFreed += bytesToFree;

                _logger?.LogInformation(
                    "Deleted episode {EpisodeId} ({VideoId}) for channel {ChannelId}, freed {Bytes} bytes",
                    episode.Id, episode.VideoId, channelId, bytesToFree);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "Failed to delete episode {EpisodeId} for channel {ChannelId}",
                    episode.Id, channelId);
                // Continue with other episodes
            }
        }

        _logger?.LogInformation(
            "Cleanup complete for channel {ChannelId}: {Count} episodes removed, {Bytes} bytes freed",
            channelId, result.EpisodesRemoved, result.BytesFreed);

        return result;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CleanupResult>> CleanupAllChannelsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<CleanupResult>();

        var enabledChannels = await _channelRepository.GetEnabledAsync(cancellationToken);

        _logger?.LogInformation(
            "Starting cleanup for {Count} enabled channels",
            enabledChannels.Count());

        foreach (var channel in enabledChannels)
        {
            try
            {
                var result = await CleanupChannelAsync(channel.Id, cancellationToken);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "Failed to cleanup channel {ChannelId}, continuing with other channels",
                    channel.Id);
                // Add empty result to track that we attempted this channel
                results.Add(new CleanupResult
                {
                    ChannelId = channel.Id,
                    EpisodesRemoved = 0,
                    BytesFreed = 0
                });
            }
        }

        var totalRemoved = results.Sum(r => r.EpisodesRemoved);
        var totalBytes = results.Sum(r => r.BytesFreed);
        _logger?.LogInformation(
            "Cleanup complete for all channels: {Count} episodes removed, {Bytes} bytes freed across {ChannelCount} channels",
            totalRemoved, totalBytes, results.Count);

        return results;
    }

    /// <summary>
    /// Deletes all media files associated with an episode.
    /// </summary>
    /// <param name="episode">The episode to delete files for.</param>
    private Task DeleteEpisodeFilesAsync(Episode episode)
    {
        // Delete audio file
        if (!string.IsNullOrEmpty(episode.FilePathAudio))
        {
            var audioPath = Path.Combine(_downloadDir, episode.FilePathAudio);
            DeleteFileSafely(audioPath);
        }

        // Delete video file
        if (!string.IsNullOrEmpty(episode.FilePathVideo))
        {
            var videoPath = Path.Combine(_downloadDir, episode.FilePathVideo);
            DeleteFileSafely(videoPath);
        }

        // Delete thumbnail if stored locally (check if ThumbnailUrl is a local path)
        if (!string.IsNullOrEmpty(episode.ThumbnailUrl) && !episode.ThumbnailUrl.StartsWith("http"))
        {
            var thumbnailPath = Path.Combine(_downloadDir, episode.ThumbnailUrl);
            DeleteFileSafely(thumbnailPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes a file, catching and logging any errors.
    /// </summary>
    /// <param name="path">The file path to delete.</param>
    private void DeleteFileSafely(string path)
    {
        try
        {
            _fileService.DeleteFile(path);
            _logger?.LogDebug("Deleted file: {Path}", path);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to delete file: {Path}", path);
            // Don't rethrow - we want to continue with other files
        }
    }
}