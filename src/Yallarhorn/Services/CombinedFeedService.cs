namespace Yallarhorn.Services;

using System.Security.Cryptography;
using System.Text;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Models;

/// <summary>
/// Service for generating combined RSS/Atom podcast feeds from all enabled channels.
/// </summary>
public class CombinedFeedService : ICombinedFeedService
{
    private const int MaxCombinedEpisodes = 100;
    private const string CombinedFeedTitle = "All Channels";
    private const string CombinedFeedDescription = "Combined feed from all channels";

    private readonly IChannelRepository _channelRepository;
    private readonly IEpisodeRepository _episodeRepository;
    private readonly IRssFeedBuilder _rssFeedBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="CombinedFeedService"/> class.
    /// </summary>
    /// <param name="channelRepository">Channel repository.</param>
    /// <param name="episodeRepository">Episode repository.</param>
    /// <param name="rssFeedBuilder">RSS feed builder.</param>
    public CombinedFeedService(
        IChannelRepository channelRepository,
        IEpisodeRepository episodeRepository,
        IRssFeedBuilder rssFeedBuilder)
    {
        _channelRepository = channelRepository;
        _episodeRepository = episodeRepository;
        _rssFeedBuilder = rssFeedBuilder;
    }

    /// <inheritdoc />
    public async Task<FeedGenerationResult> GenerateCombinedFeedAsync(
        FeedType feedType,
        CancellationToken cancellationToken = default)
    {
        // Get all enabled channels
        var enabledChannels = await _channelRepository.GetEnabledAsync(cancellationToken);

        // Collect episodes from all enabled channels
        var allEpisodes = new List<Episode>();

        foreach (var channel in enabledChannels)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // We request MaxCombinedEpisodes per channel, then limit overall
            // This ensures we have enough candidates for proper ordering
            var episodes = await _episodeRepository.GetDownloadedAsync(
                channel.Id,
                feedType,
                MaxCombinedEpisodes,
                cancellationToken);

            allEpisodes.AddRange(episodes);
        }

        // Order all episodes by published date descending and take the limit
        var orderedEpisodes = allEpisodes
            .OrderByDescending(e => e.PublishedAt ?? DateTimeOffset.MinValue)
            .Take(MaxCombinedEpisodes)
            .ToList();

        // Create a synthetic "All Channels" channel for the feed
        var syntheticChannel = CreateSyntheticChannel();

        // Build the RSS feed
        var xmlContent = _rssFeedBuilder.BuildRssFeed(
            syntheticChannel,
            orderedEpisodes,
            feedType,
            "https://localhost", // TODO: Get from configuration
            "/feeds");

        // Generate ETag from content hash
        var etag = GenerateEtag(xmlContent);

        return new FeedGenerationResult
        {
            XmlContent = xmlContent,
            Etag = etag,
            LastModified = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Creates a synthetic channel for the combined feed.
    /// </summary>
    /// <returns>A synthetic channel with combined feed metadata.</returns>
    private static Channel CreateSyntheticChannel()
    {
        return new Channel
        {
            Id = "combined",
            Title = CombinedFeedTitle,
            Url = "https://localhost", // TODO: Get from configuration
            Description = CombinedFeedDescription,
            FeedType = FeedType.Audio,
            EpisodeCountConfig = MaxCombinedEpisodes,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Generates a SHA256 ETag hash from the XML content.
    /// </summary>
    /// <param name="content">The XML content to hash.</param>
    /// <returns>Hex-encoded SHA256 hash.</returns>
    private static string GenerateEtag(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}