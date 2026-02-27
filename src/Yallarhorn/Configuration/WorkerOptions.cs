using System.ComponentModel.DataAnnotations;

namespace Yallarhorn.Configuration;

/// <summary>
/// Configuration options for background workers.
/// </summary>
public class WorkerOptions
{
    /// <summary>
    /// Gets or sets whether background workers are enabled.
    /// Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the refresh interval in seconds for the RefreshWorker.
    /// Default is 3600 seconds (1 hour).
    /// </summary>
    [Range(300, int.MaxValue, ErrorMessage = "Refresh interval must be at least 300 seconds (5 minutes)")]
    public int RefreshIntervalSeconds { get; set; } = 3600;

    /// <summary>
    /// Gets or sets the poll interval in seconds for the DownloadWorker.
    /// Default is 5 seconds.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Download poll interval must be at least 1 second")]
    public int DownloadPollIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Gets the refresh interval as a TimeSpan.
    /// </summary>
    public TimeSpan RefreshInterval => TimeSpan.FromSeconds(RefreshIntervalSeconds);

    /// <summary>
    /// Gets the download poll interval as a TimeSpan.
    /// </summary>
    public TimeSpan DownloadPollInterval => TimeSpan.FromSeconds(DownloadPollIntervalSeconds);

    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Workers";

    /// <summary>
    /// Validates all options.
    /// </summary>
    /// <returns>A collection of validation results, empty if valid.</returns>
    public IEnumerable<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this);
        Validator.TryValidateObject(this, context, results, true);
        return results;
    }
}