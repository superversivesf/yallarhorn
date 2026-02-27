using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yallarhorn.Data.Entities;

namespace Yallarhorn.Data.Configurations;

/// <summary>
/// Entity configuration for SchemaVersion.
/// </summary>
public class SchemaVersionConfiguration : IEntityTypeConfiguration<SchemaVersion>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SchemaVersion> builder)
    {
        builder.HasKey(sv => sv.Version);

        builder.Property(sv => sv.Version)
            .IsRequired();

        builder.Property(sv => sv.AppliedAt)
            .IsRequired();

        builder.Property(sv => sv.Description);

        builder.HasIndex(sv => sv.Version)
            .IsUnique();
    }
}