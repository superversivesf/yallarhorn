using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yallarhorn.Data.Entities;

namespace Yallarhorn.Data.Configurations;

/// <summary>
/// Entity configuration for Channel.
/// </summary>
public class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Url)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasIndex(c => c.Url)
            .IsUnique();

        builder.Property(c => c.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(c => c.Description);

        builder.Property(c => c.ThumbnailUrl)
            .HasMaxLength(1000);

        builder.Property(c => c.EpisodeCountConfig)
            .HasDefaultValue(50);

        builder.Property(c => c.FeedType);

        builder.Property(c => c.Enabled)
            .HasDefaultValue(true);

        builder.Property(c => c.LastRefreshAt);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired();

        builder.HasMany(c => c.Episodes)
            .WithOne(e => e.Channel)
            .HasForeignKey(e => e.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}