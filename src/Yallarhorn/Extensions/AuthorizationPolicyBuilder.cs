namespace Yallarhorn.Extensions;

using Microsoft.AspNetCore.Authorization;

/// <summary>
/// Authorization policy names for Yallarhorn.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Policy for feed endpoint access (GET /feed/*).
    /// May require authentication if feed_credentials is enabled.
    /// </summary>
    public const string FeedPolicy = "FeedPolicy";

    /// <summary>
    /// Policy for admin API endpoints (/api/v1/*).
    /// Requires authentication if admin_auth is enabled.
    /// </summary>
    public const string AdminPolicy = "AdminPolicy";

    /// <summary>
    /// Authentication scheme for feed authentication.
    /// </summary>
    public const string FeedAuthScheme = "FeedAuth";

    /// <summary>
    /// Authentication scheme for admin authentication.
    /// </summary>
    public const string AdminAuthScheme = "AdminAuth";
}

/// <summary>
/// Extension methods for building authorization policies.
/// </summary>
public static class AuthorizationPolicyBuilder
{
    /// <summary>
    /// Adds Yallarhorn authorization policies to the authorization options.
    /// </summary>
    /// <param name="options">The authorization options.</param>
    /// <returns>The authorization options for chaining.</returns>
    public static AuthorizationOptions AddYallarhornPolicies(this AuthorizationOptions options)
    {
        // Feed policy: Requires FeedAuth scheme if feed credentials are configured
        // The actual authentication requirement is handled by the authentication middleware
        options.AddPolicy(AuthorizationPolicies.FeedPolicy, policy =>
        {
            policy.RequireAuthenticatedUser()
                  .AddAuthenticationSchemes(AuthorizationPolicies.FeedAuthScheme);
        });

        // Admin policy: Requires AdminAuth scheme
        options.AddPolicy(AuthorizationPolicies.AdminPolicy, policy =>
        {
            policy.RequireAuthenticatedUser()
                  .AddAuthenticationSchemes(AuthorizationPolicies.AdminAuthScheme);
        });

        return options;
    }

    /// <summary>
    /// Adds Yallarhorn authorization policies to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddYallarhornAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddYallarhornPolicies();
        });

        return services;
    }
}