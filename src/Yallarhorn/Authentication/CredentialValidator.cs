namespace Yallarhorn.Authentication;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

/// <summary>
/// Validates credentials using BCrypt for hashed passwords or timing-safe plaintext comparison.
/// Supports domain-aware validation (e.g., "feed" vs "admin" domains).
/// </summary>
public interface ICredentialValidator
{
    /// <summary>
    /// Validates credentials against stored credentials.
    /// </summary>
    /// <param name="username">The username to validate.</param>
    /// <param name="password">The password to validate.</param>
    /// <param name="storedCredentials">Dictionary of username -> (password/hash, domain).</param>
    /// <param name="requiredDomain">Optional domain requirement. If null, no domain check is performed.</param>
    /// <returns>True if credentials are valid, false otherwise.</returns>
    Task<bool> ValidateAsync(
        string username,
        string password,
        Dictionary<string, (string Hash, string Domain)> storedCredentials,
        string? requiredDomain = null);
}

/// <summary>
/// Implementation of credential validation supporting BCrypt and plaintext comparison.
/// </summary>
public class CredentialValidator : ICredentialValidator
{
    private readonly ILogger<CredentialValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialValidator"/> class.
    /// </summary>
    public CredentialValidator(ILogger<CredentialValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateAsync(
        string username,
        string password,
        Dictionary<string, (string Hash, string Domain)> storedCredentials,
        string? requiredDomain = null)
    {
        // Validate inputs
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            LogFailedAuthentication(username ?? "null", "Empty username or password");
            return false;
        }

        if (storedCredentials == null || storedCredentials.Count == 0)
        {
            LogFailedAuthentication(username, "No credentials configured");
            return false;
        }

        // Check if user exists
        if (!storedCredentials.TryGetValue(username, out var credential))
        {
            LogFailedAuthentication(username, "User not found");
            return false;
        }

        // Validate domain if required
        if (!string.IsNullOrEmpty(requiredDomain) && credential.Domain != requiredDomain)
        {
            _logger.LogWarning(
                "Domain mismatch for user '{Username}': required '{RequiredDomain}', actual '{ActualDomain}'",
                username,
                requiredDomain,
                credential.Domain);
            return false;
        }

        // Validate password
        var passwordValid = await ValidatePasswordAsync(password, credential.Hash, username);

        if (passwordValid)
        {
            _logger.LogInformation(
                "Successfully authenticated user '{Username}' for domain '{Domain}'",
                username,
                credential.Domain);
        }
        else
        {
            LogFailedAuthentication(username, "Invalid password");
        }

        return passwordValid;
    }

    /// <summary>
    /// Validates a password against a stored hash or plaintext.
    /// Uses BCrypt verification for hashed passwords, timing-safe comparison for plaintext.
    /// </summary>
    private Task<bool> ValidatePasswordAsync(string password, string storedValue, string username)
    {
        // Detect if stored value is a BCrypt hash
        if (IsBCryptHash(storedValue))
        {
            // Use BCrypt verification
            var isValid = BCrypt.Net.BCrypt.Verify(password, storedValue);
            return Task.FromResult(isValid);
        }
        else
        {
            // Use timing-safe comparison for plaintext (env var substitution scenario)
            var isValid = TimingSafeEquals(password, storedValue);
            return Task.FromResult(isValid);
        }
    }

    /// <summary>
    /// Checks if a string appears to be a BCrypt hash.
    /// BCrypt hashes start with $2a$, $2b$, or $2y$ and are 60 characters long.
    /// </summary>
    private static bool IsBCryptHash(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 60)
        {
            return false;
        }

        return value.StartsWith("$2a$", StringComparison.Ordinal) ||
               value.StartsWith("$2b$", StringComparison.Ordinal) ||
               value.StartsWith("$2y$", StringComparison.Ordinal);
    }

    /// <summary>
    /// Performs a timing-safe comparison of two strings to prevent timing attacks.
    /// </summary>
    private static bool TimingSafeEquals(string a, string b)
    {
        if (a == null || b == null)
        {
            return a == b;
        }

        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);

        // FixedTimeEquals handles different lengths properly
        // (returns false without leaking length information)
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    /// <summary>
    /// Logs a failed authentication attempt.
    /// </summary>
    private void LogFailedAuthentication(string username, string reason)
    {
        _logger.LogWarning(
            "Failed authentication attempt for user '{Username}': {Reason}",
            username,
            reason);
    }
}