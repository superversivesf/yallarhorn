using System.ComponentModel.DataAnnotations;

namespace Yallarhorn.Configuration;

/// <summary>
/// Configuration options for the server.
/// </summary>
public class ServerOptions
{
    /// <summary>
    /// The section name in configuration.
    /// </summary>
    public const string SectionName = "Server";

    /// <summary>
    /// Gets or sets the host address the server binds to.
    /// </summary>
    public string Host { get; set; } = "0.0.0.0";

    /// <summary>
    /// Gets or sets the port the server listens on.
    /// </summary>
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Gets or sets the public URL for feed generation.
    /// Used when generating RSS/Atom feed links that clients will access.
    /// </summary>
    [Url(ErrorMessage = "Base URL must be a valid URL")]
    public string BaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>
    /// Gets or sets the URL path prefix for feed endpoints.
    /// </summary>
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Feed path must be between 1 and 100 characters")]
    public string FeedPath { get; set; } = "/feeds";

    /// <summary>
    /// Gets or sets a value indicating whether HTTPS should be used.
    /// </summary>
    public bool UseHttps { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum concurrent connections.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "MaxConcurrentConnections must be at least 1")]
    public int MaxConcurrentConnections { get; set; } = 100;

    /// <summary>
    /// Gets or sets the request timeout in seconds.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "RequestTimeoutSeconds must be at least 1")]
    public int RequestTimeoutSeconds { get; set; } = 30;

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
                $"Server options validation failed: {string.Join(", ", results.Select(r => r.ErrorMessage))}");
        }
    }

    /// <summary>
    /// Gets the full feed URL by combining BaseUrl and FeedPath.
    /// </summary>
    /// <returns>The full feed URL.</returns>
    public string GetFullFeedUrl()
    {
        var baseUrl = BaseUrl.TrimEnd('/');
        var feedPath = FeedPath.StartsWith('/') ? FeedPath : "/" + FeedPath;
        return $"{baseUrl}{feedPath}";
    }
}