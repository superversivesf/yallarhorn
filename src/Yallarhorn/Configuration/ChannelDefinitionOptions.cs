using System.ComponentModel.DataAnnotations;

namespace Yallarhorn.Configuration;

/// <summary>
/// Configuration options for a YouTube channel to monitor.
/// </summary>
public class ChannelDefinitionOptions
{
    /// <summary>
    /// Gets or sets the display name for the channel.
    /// </summary>
    [Required(ErrorMessage = "Channel name is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Channel name must be between 1 and 200 characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the YouTube channel URL.
    /// </summary>
    [Required(ErrorMessage = "Channel URL is required")]
    [Url(ErrorMessage = "Channel URL must be a valid URL")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of episodes to keep (rolling window).
    /// </summary>
    [Range(1, 1000, ErrorMessage = "Episode count must be between 1 and 1000")]
    public int EpisodeCount { get; set; } = 50;

    /// <summary>
    /// Gets or sets a value indicating whether this channel is actively monitored.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the feed output type (audio, video, or both).
    /// </summary>
    [AllowedValues("audio", "video", "both")]
    public string FeedType { get; set; } = "audio";

    /// <summary>
    /// Gets or sets custom transcoding settings for this channel.
    /// </summary>
    public TranscodeOptions? CustomSettings { get; set; }

    /// <summary>
    /// Gets or sets custom channel description.
    /// </summary>
    [StringLength(2000, ErrorMessage = "Description must be at most 2000 characters")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets tags for categorization.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Validates the options and returns validation results.
    /// </summary>
    /// <returns>A collection of validation results, empty if valid.</returns>
    public IEnumerable<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this);
        Validator.TryValidateObject(this, context, results, true);

        // Validate URL is a YouTube channel URL
        if (!IsValidYouTubeChannelUrl(Url))
        {
            results.Add(new ValidationResult("Invalid YouTube channel URL format. Must be a channel URL, not a video or playlist URL.", new[] { nameof(Url) }));
        }

        // Validate custom settings if provided
        if (CustomSettings != null)
        {
            results.AddRange(CustomSettings.Validate());
        }

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
                $"Channel '{Name}' validation failed: {string.Join(", ", results.Select(r => r.ErrorMessage))}");
        }
    }

    /// <summary>
    /// Validates that the URL is a valid YouTube channel URL format.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <returns>True if the URL is a valid YouTube channel URL.</returns>
    private static bool IsValidYouTubeChannelUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        // Must use HTTPS
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;

        // Must be youtube.com
        if (!url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
            return false;

        // Must not be a video or playlist URL
        if (url.Contains("/watch", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("playlist", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("list=", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("/v/", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("watch?v=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Valid channel URL patterns:
        // - https://www.youtube.com/@channelname
        // - https://www.youtube.com/c/channelname
        // - https://www.youtube.com/channel/UC...
        // - https://www.youtube.com/user/username
        return true;
    }
}