using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Yallarhorn.Data.Entities;

/// <summary>
/// Represents a schema migration version record.
/// </summary>
[Table("schema_version")]
public class SchemaVersion
{
    /// <summary>
    /// The schema version number (primary key).
    /// </summary>
    [Key]
    [Column("version")]
    public int Version { get; set; }

    /// <summary>
    /// When this migration was applied.
    /// </summary>
    [Required]
    [Column("applied_at")]
    public DateTimeOffset AppliedAt { get; set; }

    /// <summary>
    /// Description of the migration.
    /// </summary>
    [Column("description")]
    public string? Description { get; set; }
}