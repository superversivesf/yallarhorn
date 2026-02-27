using System.ComponentModel.DataAnnotations;

namespace Yallarhorn.Configuration;

/// <summary>
/// Configuration options for HTTP Basic Auth credentials on feed endpoints.
/// </summary>
public class FeedCredentials
{
    /// <summary>
    /// Gets or sets a value indicating whether feed authentication is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the username for feed access.
    /// Required when Enabled is true.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password for feed access.
    /// Required when Enabled is true.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the HTTP auth realm.
    /// </summary>
    public string Realm { get; set; } = "Yallarhorn Feeds";

    /// <summary>
    /// Validates the options and returns validation results.
    /// </summary>
    /// <returns>A collection of validation results, empty if valid.</returns>
    public IEnumerable<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();

        if (Enabled)
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                results.Add(new ValidationResult("Username is required when feed_credentials is enabled", new[] { nameof(Username) }));
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                results.Add(new ValidationResult("Password is required when feed_credentials is enabled", new[] { nameof(Password) }));
            }
        }

        return results;
    }
}

/// <summary>
/// Configuration options for HTTP Basic Auth credentials on admin API endpoints.
/// </summary>
public class AdminAuth
{
    /// <summary>
    /// Gets or sets a value indicating whether admin authentication is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the username for admin access.
    /// Required when Enabled is true.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password for admin access.
    /// Required when Enabled is true.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Validates the options and returns validation results.
    /// </summary>
    /// <returns>A collection of validation results, empty if valid.</returns>
    public IEnumerable<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();

        if (Enabled)
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                results.Add(new ValidationResult("Username is required when admin_auth is enabled", new[] { nameof(Username) }));
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                results.Add(new ValidationResult("Password is required when admin_auth is enabled", new[] { nameof(Password) }));
            }
        }

        return results;
    }
}

/// <summary>
/// Configuration options for authentication.
/// </summary>
public class AuthOptions
{
    /// <summary>
    /// The section name in configuration.
    /// </summary>
    public const string SectionName = "Auth";

    /// <summary>
    /// Gets or sets the feed credentials for HTTP Basic Auth on feed endpoints.
    /// </summary>
    public FeedCredentials FeedCredentials { get; set; } = new();

    /// <summary>
    /// Gets or sets the admin auth for HTTP Basic Auth on admin API endpoints.
    /// </summary>
    public AdminAuth AdminAuth { get; set; } = new();

    /// <summary>
    /// Validates the options and returns validation results.
    /// </summary>
    /// <returns>A collection of validation results, empty if valid.</returns>
    public IEnumerable<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();
        results.AddRange(FeedCredentials.Validate());
        results.AddRange(AdminAuth.Validate());
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
                $"Auth options validation failed: {string.Join(", ", results.Select(r => r.ErrorMessage))}");
        }
    }
}