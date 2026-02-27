namespace Yallarhorn.Authentication;

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// HTTP Basic Authentication handler for ASP.NET Core.
/// </summary>
public class BasicAuthHandler : AuthenticationHandler<BasicAuthOptions>
{
    private const string AuthorizationHeaderName = "Authorization";
    private const string BasicScheme = "Basic";

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicAuthHandler"/> class.
    /// </summary>
    public BasicAuthHandler(
        IOptionsMonitor<BasicAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for Authorization header
        if (!Request.Headers.TryGetValue(AuthorizationHeaderName, out var authorizationHeaderValues))
        {
            Logger.LogDebug("No Authorization header found");
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var authorizationHeader = authorizationHeaderValues.ToString();
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            Logger.LogDebug("Empty Authorization header");
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Verify scheme is "Basic"
        if (!authorizationHeader.StartsWith($"{BasicScheme} ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogDebug("Authorization header does not use Basic scheme");
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Extract and decode credentials
        var encodedCredentials = authorizationHeader.Substring(BasicScheme.Length).Trim();
        string decodedCredentials;
        
        try
        {
            var credentialBytes = Convert.FromBase64String(encodedCredentials);
            decodedCredentials = Encoding.UTF8.GetString(credentialBytes);
        }
        catch (FormatException ex)
        {
            Logger.LogWarning(ex, "Invalid Base64 encoding in Authorization header");
            return Task.FromResult(AuthenticateResult.Fail("Invalid Base64 encoding"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error decoding credentials");
            return Task.FromResult(AuthenticateResult.Fail("Error decoding credentials"));
        }

        // Parse username:password
        var colonIndex = decodedCredentials.IndexOf(':');
        if (colonIndex < 0)
        {
            Logger.LogWarning("Credentials do not contain a colon separator");
            return Task.FromResult(AuthenticateResult.Fail("Invalid credentials format"));
        }

        var username = decodedCredentials.Substring(0, colonIndex);
        var password = decodedCredentials.Substring(colonIndex + 1);

        // Validate credentials are not empty
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Logger.LogWarning("Empty username or password");
            return Task.FromResult(AuthenticateResult.Fail("Empty username or password"));
        }

        // Validate against configured credentials
        if (!IsValidCredentials(username, password))
        {
            Logger.LogWarning("Invalid credentials for user '{Username}'", username);
            return Task.FromResult(AuthenticateResult.Fail("Invalid credentials"));
        }

        // Create claims principal
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.NameIdentifier, username)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);

        Logger.LogInformation("Successfully authenticated user '{Username}'", username);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }

    /// <inheritdoc/>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers["WWW-Authenticate"] = $"{BasicScheme} realm=\"{Options.Realm}\"";
        await base.HandleChallengeAsync(properties);
    }

    /// <summary>
    /// Validates the provided credentials against configured credentials.
    /// Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    private bool IsValidCredentials(string username, string password)
    {
        // Check if credentials are configured
        if (string.IsNullOrEmpty(Options.Username) || string.IsNullOrEmpty(Options.Password))
        {
            Logger.LogWarning("No credentials configured for Basic Auth");
            return false;
        }

        // Use constant-time comparison for security
        var usernameValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(username),
            Encoding.UTF8.GetBytes(Options.Username));

        var passwordValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(password),
            Encoding.UTF8.GetBytes(Options.Password));

        return usernameValid && passwordValid;
    }
}