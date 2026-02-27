using Microsoft.EntityFrameworkCore;
using Yallarhorn.Data.Configurations;
using Yallarhorn.Data.Entities;

namespace Yallarhorn.Data;

/// <summary>
/// Entity Framework DbContext for Yallarhorn.
/// </summary>
public class YallarhornDbContext : DbContext
{
    /// <summary>
    /// Gets the Channels DbSet.
    /// </summary>
    public DbSet<Channel> Channels => Set<Channel>();

    /// <summary>
    /// Gets the Episodes DbSet.
    /// </summary>
    public DbSet<Episode> Episodes => Set<Episode>();

    /// <summary>
    /// Gets the DownloadQueue DbSet.
    /// </summary>
    public DbSet<DownloadQueue> DownloadQueue => Set<DownloadQueue>();

    /// <summary>
    /// Gets the SchemaVersions DbSet.
    /// </summary>
    public DbSet<SchemaVersion> SchemaVersions => Set<SchemaVersion>();

    /// <summary>
    /// Initializes a new instance of the <see cref="YallarhornDbContext"/> class.
    /// </summary>
    /// <param name="options">The dbContext options.</param>
    public YallarhornDbContext(DbContextOptions<YallarhornDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ChannelConfiguration());
        modelBuilder.ApplyConfiguration(new EpisodeConfiguration());
        modelBuilder.ApplyConfiguration(new DownloadQueueConfiguration());
        modelBuilder.ApplyConfiguration(new SchemaVersionConfiguration());
    }
}