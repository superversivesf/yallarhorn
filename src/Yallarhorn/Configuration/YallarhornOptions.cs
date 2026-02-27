using System.ComponentModel.DataAnnotations;

namespace Yallarhorn.Configuration;

/// <summary>
/// Root configuration options for Yallarhorn.
/// Contains all top-level settings and nested configuration sections.
/// </summary>
public class YallarhornOptions
{
    /// <summary>
    /// Gets or sets the configuration schema version.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Gets or sets the interval between channel refresh cycles in seconds.
    /// </summary>
    [Range(300, int.MaxValue, ErrorMessage = "Poll interval must be at least 300 seconds (5 minutes)")]
    public int PollInterval { get; set; } = 3600;

    /// <summary>
    /// Gets or sets the maximum number of simultaneous downloads.
    /// </summary>
    [Range(1, 10, ErrorMessage = "Max concurrent downloads must be between 1 and 10")]
    public int MaxConcurrentDownloads { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base directory for downloaded content.
    /// </summary>
    [Required(ErrorMessage = "Download directory is required")]
    public string DownloadDir { get; set; } = "./downloads";

    /// <summary>
    /// Gets or sets the temporary file directory during processing.
    /// </summary>
    [Required(ErrorMessage = "Temp directory is required")]
    public string TempDir { get; set; } = "./temp";

    /// <summary>
    /// Gets or sets the transcode settings for media processing.
    /// </summary>
    public TranscodeOptions TranscodeSettings { get; set; } = new();

    /// <summary>
    /// Gets or sets the server configuration.
    /// </summary>
    public ServerOptions Server { get; set; } = new();

    /// <summary>
    /// Gets or sets the database configuration.
    /// </summary>
    public DatabaseOptions Database { get; set; } = new();

    /// <summary>
    /// Gets or sets the authentication configuration.
    /// </summary>
    public AuthOptions Auth { get; set; } = new();

    /// <summary>
    /// Gets or sets the background worker configuration.
    /// </summary>
    public WorkerOptions Workers { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of channels to monitor.
    /// </summary>
    public List<ChannelDefinitionOptions> Channels { get; set; } = new();

    /// <summary>
    /// Validates all options including nested sections.
    /// </summary>
    /// <returns>A collection of validation results, empty if valid.</returns>
    public IEnumerable<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this);
        Validator.TryValidateObject(this, context, results, true);

        // Validate nested options
        results.AddRange(TranscodeSettings.Validate());
        results.AddRange(Server.Validate());
        results.AddRange(Database.Validate());
        results.AddRange(Auth.Validate());
        results.AddRange(Workers.Validate());

        // Validate each channel
        for (int i = 0; i < Channels.Count; i++)
        {
            var channelResults = Channels[i].Validate();
            foreach (var result in channelResults)
            {
                results.Add(new ValidationResult(
                    $"channels[{i}]: {result.ErrorMessage}",
                    result.MemberNames));
            }
        }

        // Must have at least one channel
        if (Channels.Count == 0)
        {
            results.Add(new ValidationResult("At least one channel must be defined", new[] { nameof(Channels) }));
        }

        return results;
    }

    /// <summary>
    /// Validates all options and throws if invalid.
    /// </summary>
    public void ValidateAndThrow()
    {
        var results = Validate().ToList();
        if (results.Count != 0)
        {
            throw new ValidationException(
                $"Configuration validation failed:\n  - {string.Join("\n  - ", results.Select(r => r.ErrorMessage))}");
        }
    }

    /// <summary>
    /// Gets all enabled channels.
    /// </summary>
    /// <returns>A list of enabled channels.</returns>
    public List<ChannelDefinitionOptions> GetEnabledChannels()
    {
        return Channels.Where(c => c.Enabled).ToList();
    }
}