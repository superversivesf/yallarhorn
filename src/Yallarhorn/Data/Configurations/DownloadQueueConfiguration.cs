using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yallarhorn.Data.Entities;

namespace Yallarhorn.Data.Configurations;

/// <summary>
/// Entity configuration for DownloadQueue.
/// </summary>
public class DownloadQueueConfiguration : IEntityTypeConfiguration<DownloadQueue>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DownloadQueue> builder)
    {
        builder.HasKey(dq => dq.Id);

        builder.Property(dq => dq.Id)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(dq => dq.EpisodeId)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(dq => dq.EpisodeId)
            .IsUnique();

        builder.Property(dq => dq.Priority)
            .HasDefaultValue(5);

        builder.HasIndex(dq => new { dq.Status, dq.Priority });

        builder.Property(dq => dq.Status);

        builder.Property(dq => dq.Attempts)
            .HasDefaultValue(0);

        builder.Property(dq => dq.MaxAttempts)
            .HasDefaultValue(3);

        builder.Property(dq => dq.LastError);

        builder.Property(dq => dq.NextRetryAt);

        builder.HasIndex(dq => dq.NextRetryAt);

        builder.Property(dq => dq.CreatedAt)
            .IsRequired();

        builder.Property(dq => dq.UpdatedAt)
            .IsRequired();

        builder.HasOne(dq => dq.Episode)
            .WithOne(e => e.DownloadQueue)
            .HasForeignKey<DownloadQueue>(dq => dq.EpisodeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}