using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yallarhorn.Data.Entities;

namespace Yallarhorn.Data.Configurations;

/// <summary>
/// Entity configuration for Episode.
/// </summary>
public class EpisodeConfiguration : IEntityTypeConfiguration<Episode>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Episode> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.VideoId)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(e => e.VideoId)
            .IsUnique();

        builder.Property(e => e.ChannelId)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(e => e.ChannelId);

        builder.Property(e => e.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.Description);

        builder.Property(e => e.ThumbnailUrl)
            .HasMaxLength(1000);

        builder.Property(e => e.DurationSeconds);

        builder.Property(e => e.PublishedAt);

        builder.Property(e => e.DownloadedAt);

        builder.Property(e => e.FilePathAudio)
            .HasMaxLength(500);

        builder.Property(e => e.FilePathVideo)
            .HasMaxLength(500);

        builder.Property(e => e.FileSizeAudio);

        builder.Property(e => e.FileSizeVideo);

        builder.Property(e => e.Status);

        builder.HasIndex(e => e.Status);

        builder.Property(e => e.RetryCount)
            .HasDefaultValue(0);

        builder.Property(e => e.ErrorMessage);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired();

        builder.HasOne(e => e.Channel)
            .WithMany(c => c.Episodes)
            .HasForeignKey(e => e.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.DownloadQueue)
            .WithOne(dq => dq.Episode)
            .HasForeignKey<DownloadQueue>(dq => dq.EpisodeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.ChannelId, e.PublishedAt });
    }
}