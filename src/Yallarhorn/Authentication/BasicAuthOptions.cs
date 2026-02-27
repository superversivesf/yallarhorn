namespace Yallarhorn.Authentication;

using Microsoft.AspNetCore.Authentication;

/// <summary>
/// Options for HTTP Basic Authentication handler.
/// </summary>
public class BasicAuthOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Gets or sets the username for authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password for authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the HTTP auth realm used in WWW-Authenticate header.
    /// </summary>
    public string Realm { get; set; } = "Restricted";
}