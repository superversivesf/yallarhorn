namespace Yallarhorn.Services;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Yallarhorn.Configuration;
using Yallarhorn.Data.Entities;
using Yallarhorn.Data.Enums;
using Yallarhorn.Data.Repositories;
using Yallarhorn.Models;

/// <summary>
/// Service for generating RSS/Atom podcast feeds per channel.
/// </summary>
public class FeedService : IFeedService
{
    private const int DefaultEpisodeCount = 50;
    private readonly IChannelRepository _channelRepository;
    private readonly IEpisodeRepository _episodeRepository;
    private readonly IRssFeedBuilder _rssFeedBuilder;
    private readonly IAtomFeedBuilder _atomFeedBuilder;
    private readonly ServerOptions _serverOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeedService"/> class.
    /// </summary>
    /// <param name="channelRepository">Channel repository.</param>
    /// <param name="episodeRepository">Episode repository.</param>
    /// <param name="rssFeedBuilder">RSS feed builder.</param>
    /// <param name="atomFeedBuilder">Atom feed builder.</param>
    /// <param name="serverOptions">Server configuration options.</param>
    public FeedService(
        IChannelRepository channelRepository,
        IEpisodeRepository episodeRepository,
        IRssFeedBuilder rssFeedBuilder,
        IAtomFeedBuilder atomFeedBuilder,
        IOptions<ServerOptions> serverOptions)
    {
        _channelRepository = channelRepository;
        _episodeRepository = episodeRepository;
        _rssFeedBuilder = rssFeedBuilder;
        _atomFeedBuilder = atomFeedBuilder;
        _serverOptions = serverOptions.Value;
    }

    /// <inheritdoc />
    public async Task<FeedGenerationResult?> GenerateFeedAsync(
        string channelId,
        FeedType feedType,
        CancellationToken cancellationToken = default)
    {
        // Get the channel
        var channel = await _channelRepository.GetByIdAsync(channelId, cancellationToken);
        if (channel == null)
        {
            return null;
        }

        // Determine the episode count limit
        var episodeCount = channel.EpisodeCountConfig > 0 
            ? channel.EpisodeCountConfig 
            : DefaultEpisodeCount;

        // Get downloaded episodes for the specified feed type
        var episodes = await _episodeRepository.GetDownloadedAsync(
            channelId,
            feedType,
            episodeCount,
            cancellationToken);

        // Build the RSS feed
        var xmlContent = _rssFeedBuilder.BuildRssFeed(
            channel,
            episodes,
            feedType,
            _serverOptions.BaseUrl,
            _serverOptions.FeedPath);

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