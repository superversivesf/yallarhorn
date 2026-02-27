using Microsoft.EntityFrameworkCore;
using Yallarhorn.Data;
using Yallarhorn.Data.Entities;

namespace Yallarhorn.Data.Seeding;

/// <summary>
/// Seeds channels from YAML configuration into the database.
/// </summary>
public class ChannelSeeder
{
    private readonly YallarhornDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelSeeder"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public ChannelSeeder(YallarhornDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Seeds channels from configuration definitions.
    /// </summary>
    /// <param name="channelDefinitions">Channel definitions to seed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple of (added count, updated count, skipped count).</returns>
    public async Task<(int Added, int Updated, int Skipped)> SeedAsync(
        IEnumerable<ChannelDefinition> channelDefinitions,
        CancellationToken cancellationToken = default)
    {
        var added = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var definition in channelDefinitions)
        {
            var existingChannel = await _context.Channels
                .FirstOrDefaultAsync(c => c.Url == definition.Url, cancellationToken);

            if (existingChannel == null)
            {
                var channel = new Channel
                {
                    Id = Guid.NewGuid().ToString(),
                    Url = definition.Url,
                    Title = definition.Title,
                    Description = definition.Description,
                    ThumbnailUrl = definition.ThumbnailUrl,
                    EpisodeCountConfig = definition.EpisodeCount,
                    FeedType = definition.FeedType,
                    Enabled = definition.Enabled,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                _context.Channels.Add(channel);
                added++;
            }
            else
            {
                if (definition.UpdateIfExists)
                {
                    existingChannel.Title = definition.Title;
                    existingChannel.Description = definition.Description ?? existingChannel.Description;
                    existingChannel.EpisodeCountConfig = definition.EpisodeCount;
                    existingChannel.FeedType = definition.FeedType;
                    existingChannel.Enabled = definition.Enabled;
                    existingChannel.UpdatedAt = DateTimeOffset.UtcNow;
                    updated++;
                }
                else
                {
                    skipped++;
                }
            }
        }

        if (added > 0 || updated > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return (added, updated, skipped);
    }
}

/// <summary>
/// Represents a channel definition for seeding.
/// </summary>
public class ChannelDefinition
{
    /// <summary>
    /// Gets or sets the channel URL.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Gets or sets the channel title.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Gets or sets the channel description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the thumbnail URL.
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Gets or sets the episode count.
    /// </summary>
    public int EpisodeCount { get; set; } = 50;

    /// <summary>
    /// Gets or sets the feed type.
    /// </summary>
    public Data.Enums.FeedType FeedType { get; set; } = Data.Enums.FeedType.Audio;

    /// <summary>
    /// Gets or sets whether the channel is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to update if exists.
    /// </summary>
    public bool UpdateIfExists { get; set; } = false;
}