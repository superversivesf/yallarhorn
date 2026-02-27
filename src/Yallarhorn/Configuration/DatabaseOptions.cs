using System.ComponentModel.DataAnnotations;

namespace Yallarhorn.Configuration;

/// <summary>
/// Configuration options for database settings.
/// </summary>
public class DatabaseOptions
{
    /// <summary>
    /// The section name in configuration.
    /// </summary>
    public const string SectionName = "Database";

    /// <summary>
    /// Gets or sets the SQLite database file path.
    /// </summary>
    [Required(ErrorMessage = "Database path is required")]
    public string Path { get; set; } = "./yallarhorn.db";

    /// <summary>
    /// Gets or sets the connection pool size.
    /// </summary>
    [Range(1, 100, ErrorMessage = "Pool size must be between 1 and 100")]
    public int PoolSize { get; set; } = 5;

    /// <summary>
    /// Validates the options and returns validation results.
    /// </summary>
    /// <returns>A collection of validation results, empty if valid.</returns>
    public IEnumerable<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this);
        Validator.TryValidateObject(this, context, results, true);
        return results;
    }

    /// <summary>
    /// Validates the options and throws if invalid.
    /// </summary>
    public void ValidateAndThrow()
    {
        var results = Validate().ToList();
        if (results.Count != 0)
        {
            throw new ValidationException(
                $"Database options validation failed: {string.Join(", ", results.Select(r => r.ErrorMessage))}");
        }
    }
}