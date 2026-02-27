# Authentication Design

This document describes Yallarhorn's dual authentication system, which provides separate access control for RSS/Atom feed consumption and the management API.

## Table of Contents

- [Overview](#overview)
- [Dual Authentication Model](#dual-authentication-model)
- [HTTP Basic Auth Implementation](#http-basic-auth-implementation)
- [Credential Storage](#credential-storage)
- [Middleware Design](#middleware-design)
- [Security Considerations](#security-considerations)
- [Configuration Reference](#configuration-reference)

---

## Overview

Yallarhorn implements a dual authentication strategy to support two distinct access patterns:

1. **Feed Authentication** (`feed_auth`): Secures RSS/Atom feed endpoints consumed by podcast clients
2. **Admin Authentication** (`admin_auth`): Secures the management API for administrative operations

### No YouTube API Required

Yallarhorn does **not** require YouTube API keys or OAuth credentials. The application uses yt-dlp to extract video metadata directly from YouTube, eliminating the need for:

- YouTube Data API quotas and limits
- OAuth 2.0 client configuration
- API key management and rotation
- Rate limiting concerns from YouTube's API

This design choice simplifies deployment and removes external API dependencies.

---

## Dual Authentication Model

### Authentication Domains

| Domain | Protected Paths | Purpose | Permissions |
|--------|-----------------|---------|-------------|
| `feed_auth` | `/feed/*`, `/feeds/*` | Podcast client access | Read-only (RSS feeds, media files) |
| `admin_auth` | `/api/*` | Management interface | Read-write (channels, episodes, triggers) |

### Separate Credentials

Each domain uses independent credentials:

```yaml
auth:
  feed_credentials:
    enabled: true
    username: "feed-user"
    password: "${FEED_PASSWORD}"
    
  admin_auth:
    enabled: true
    username: "admin"
    password: "${ADMIN_PASSWORD}"
```

### Why Separate Credentials?

1. **Principle of Least Privilege**: Podcast clients receive read-only credentials that cannot modify server state
2. **Credential Rotation**: Feed passwords can be rotated independently of admin credentials
3. **Compromise Isolation**: If feed credentials are leaked (shared with podcast apps), admin access remains secure
4. **Audit Trail**: Distinct credentials enable differentiated logging and monitoring

### Endpoint Protection Matrix

| Endpoint Pattern | Feed Auth | Admin Auth | Public |
|------------------|-----------|------------|--------|
| `/feed/{id}/audio.rss` | Required | - | No |
| `/feed/{id}/video.rss` | Required | - | No |
| `/feeds/{channel}/{type}/{file}` | Required | - | No |
| `/feeds/all.rss` | Required | - | No |
| `/api/v1/channels` | - | Required | No |
| `/api/v1/episodes` | - | Required | No |
| `/api/v1/status` | - | Required | No |
| `/api/v1/health` | - | - | Yes |
| `/api/v1/refresh-all` | - | Required | No |

### Health Endpoint Exception

The `/api/v1/health` endpoint is intentionally public to support:
- Load balancer health checks
- Container orchestration probes (Kubernetes, Docker Swarm)
- Monitoring system checks

---

## HTTP Basic Auth Implementation

### Protocol Overview

Yallarhorn uses HTTP Basic Authentication as defined in [RFC 7617](https://tools.ietf.org/html/rfc7617).

#### Request Format

```
Authorization: Basic <base64(username:password)>
```

The client Base64-encodes the string `username:password` and includes it in the `Authorization` header.

#### ASP.NET Core Implementation

```csharp
public static class BasicAuthExtensions
{
    public static IServiceCollection AddBasicAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication()
            .AddScheme<BasicAuthOptions, BasicAuthHandler>("BasicAuth", null);
        
        services.AddAuthorization(options =>
        {
            options.AddPolicy("FeedAuth", policy =>
                policy.RequireAuthenticatedUser()
                      .AddAuthenticationSchemes("BasicAuth"));
            
            options.AddPolicy("AdminAuth", policy =>
                policy.RequireAuthenticatedUser()
                      .AddAuthenticationSchemes("BasicAuth"));
        });
        
        return services;
    }
}
```

### Basic Auth Handler

```csharp
public class BasicAuthHandler : AuthenticationHandler<BasicAuthOptions>
{
    private readonly ICredentialValidator _credentialValidator;
    
    public BasicAuthHandler(
        IOptionsMonitor<BasicAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ICredentialValidator credentialValidator)
        : base(options, logger, encoder)
    {
        _credentialValidator = credentialValidator;
    }
    
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Skip for public endpoints
        if (IsPublicEndpoint())
        {
            return AuthenticateResult.NoResult();
        }
        
        // Extract Authorization header
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return AuthenticateResult.Fail("Missing Authorization header");
        }
        
        var authValue = authHeader.ToString();
        if (!authValue.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Invalid authentication scheme");
        }
        
        // Decode credentials
        var encodedCredentials = authValue.Substring("Basic ".Length).Trim();
        string username, password;
        
        try
        {
            var decodedBytes = Convert.FromBase64String(encodedCredentials);
            var decodedCredentials = Encoding.UTF8.GetString(decodedBytes);
            var separatorIndex = decodedCredentials.IndexOf(':');
            
            if (separatorIndex == -1)
            {
                return AuthenticateResult.Fail("Invalid credential format");
            }
            
            username = decodedCredentials.Substring(0, separatorIndex);
            password = decodedCredentials.Substring(separatorIndex + 1);
        }
        catch (FormatException)
        {
            return AuthenticateResult.Fail("Invalid Base64 encoding");
        }
        
        // Determine required auth domain based on path
        var authDomain = DetermineAuthDomain(Request.Path);
        
        // Validate credentials
        var isValid = await _credentialValidator.ValidateAsync(
            username, password, authDomain);
        
        if (!isValid)
        {
            return AuthenticateResult.Fail("Invalid credentials");
        }
        
        // Create principal
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim("AuthDomain", authDomain)
        };
        
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        
        return AuthenticateResult.Success(
            new AuthenticationTicket(principal, Scheme.Name));
    }
    
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.Append("WWW-Authenticate", 
            $"Basic realm=\"{Options.Realm}\", charset=\"UTF-8\"");
        
        await Response.WriteAsJsonAsync(new
        {
            error = new
            {
                code = "UNAUTHORIZED",
                message = "Authentication required",
                details = "Include valid credentials in the Authorization header"
            }
        });
    }
    
    private string DetermineAuthDomain(PathString path)
    {
        if (path.StartsWithSegments("/feed") || path.StartsWithSegments("/feeds"))
            return "feed";
        if (path.StartsWithSegments("/api"))
            return "admin";
        return "none";
    }
    
    private bool IsPublicEndpoint()
    {
        return Request.Path.StartsWithSegments("/api/v1/health");
    }
}
```

### Credential Validator

```csharp
public interface ICredentialValidator
{
    Task<bool> ValidateAsync(string username, string password, string authDomain);
}

public class BCryptCredentialValidator : ICredentialValidator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BCryptCredentialValidator> _logger;
    
    public BCryptCredentialValidator(
        IConfiguration configuration,
        ILogger<BCryptCredentialValidator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }
    
    public Task<bool> ValidateAsync(string username, string password, string authDomain)
    {
        var configSection = authDomain switch
        {
            "feed" => _configuration.GetSection("auth:feed_credentials"),
            "admin" => _configuration.GetSection("auth:admin_auth"),
            _ => null
        };
        
        if (configSection == null)
        {
            _logger.LogWarning("Unknown auth domain: {AuthDomain}", authDomain);
            return Task.FromResult(false);
        }
        
        var enabled = configSection.GetValue<bool>("enabled");
        if (!enabled)
        {
            // Authentication disabled for this domain
            return Task.FromResult(true);
        }
        
        var configuredUsername = configSection["username"];
        var configuredPasswordHash = configSection["password_hash"];
        
        // Compare username (case-sensitive)
        if (!string.Equals(username, configuredUsername, StringComparison.Ordinal))
        {
            _logger.LogDebug("Username mismatch for {AuthDomain}", authDomain);
            return Task.FromResult(false);
        }
        
        // Verify password using BCrypt
        bool isValid;
        if (!string.IsNullOrEmpty(configuredPasswordHash))
        {
            // Hashed password
            isValid = BCrypt.Net.BCrypt.Verify(password, configuredPasswordHash);
        }
        else
        {
            // Plaintext fallback (for environment variable substitution)
            var configuredPassword = configSection["password"];
            isValid = string.Equals(password, configuredPassword, StringComparison.Ordinal);
            
            if (isValid)
            {
                _logger.LogWarning(
                    "Using plaintext password for {AuthDomain}. " +
                    "Consider storing hashed passwords for production.",
                    authDomain);
            }
        }
        
        return Task.FromResult(isValid);
    }
}
```

---

## Credential Storage

### Configuration-Based Storage

Yallarhorn stores credentials in the YAML configuration file with support for both plaintext and hashed formats.

#### Plaintext with Environment Variables

```yaml
auth:
  feed_credentials:
    enabled: true
    username: "feed-user"
    password: "${FEED_PASSWORD}"    # Loaded from environment
    
  admin_auth:
    enabled: true
    username: "admin"
    password: "${ADMIN_PASSWORD}"   # Loaded from environment
```

#### Hashed Passwords (Recommended)

```yaml
auth:
  feed_credentials:
    enabled: true
    username: "feed-user"
    password_hash: "$2a$11$N9qo8uLOickgx2ZMRZoMy..."  # BCrypt hash
    
  admin_auth:
    enabled: true
    username: "admin"
    password_hash: "$2a$11$rF8XQy3NlZK5eW8..."        # BCrypt hash
```

### BCrypt Hashing

Yallarhorn uses BCrypt for password hashing, selected for its:

- **Built-in salting**: Each hash includes a unique salt automatically
- **Adaptive cost**: Adjustable work factor to counter increasing compute power
- **Time-tested**: Mature algorithm with proven security record
- **Wide library support**: Available in all major programming languages

#### Hash Format

BCrypt hashes follow the Modular Crypt Format:

```
$2a$11$N9qo8uLOickgx2ZMRZoMy.Mrq7mK9p5r7mK9p5r7mK9p5r7mK9p5r7mK
│  │  │                                                        │
│  │  │                                                        └── Hash (31 chars)
│  │  └── Salt (22 chars)
│  └── Cost factor (2 digits, 04-31)
└── Algorithm version
```

#### Generating BCrypt Hashes

**Using the CLI:**

```bash
# Generate a new password hash
yallarhorn auth hash-password

# Output: Enter password: ********
# Output: $2a$11$N9qo8uLOickgx2ZMRZoMy...
```

**Programmatically (C#):**

```csharp
using BCrypt.Net;

// Generate hash with default work factor (11)
string hash = BCrypt.Net.BCrypt.HashPassword(password);

// Generate hash with custom work factor
string hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

// Verify password
bool isValid = BCrypt.Net.BCrypt.Verify(password, hash);
```

**Using htpasswd (Apache):**

```bash
htpasswd -nbBC 12 "" "my-password"
# Output: :$2y$12$N9qo8uLOickgx2ZMRZoMy...
# Remove the leading colon to get just the hash
```

### Work Factor Selection

| Work Factor | Hash Time | Recommendation |
|-------------|-----------|----------------|
| 10 | ~100ms | Minimum acceptable |
| 11 | ~200ms | **Recommended default** |
| 12 | ~400ms | High-security environments |
| 13 | ~800ms | Sensitive systems |
| 14 | ~1.6s | May impact performance |

The work factor should be calibrated to take ~200-500ms on target hardware, balancing security against authentication latency.

### Environment Variable Integration

For production deployments, load passwords from environment variables:

```bash
# .env file
FEED_PASSWORD=secure-feed-password-here
ADMIN_PASSWORD=secure-admin-password-here
```

```yaml
# yallarhorn.yaml
auth:
  feed_credentials:
    enabled: true
    username: "feed-user"
    password: "${FEED_PASSWORD}"
    
  admin_auth:
    enabled: true
    username: "admin"
    password: "${ADMIN_PASSWORD}"
```

### Password Policy Recommendation

For production deployments:

1. **Minimum length**: 16 characters for admin, 12 for feed access
2. **Composition**: Mix of uppercase, lowercase, digits, and symbols
3. **Rotation**: Rotate feed credentials quarterly, admin monthly
4. **Storage**: Use hashed format (`password_hash`) rather than plaintext

---

## Middleware Design

### Middleware Pipeline

The authentication middleware is positioned early in the ASP.NET Core request pipeline:

```
Request
   │
   ▼
HTTPS Redirection (production)
   │
   ▼
Exception Handling
   │
   ▼
[Authentication Middleware]  ←── Validates credentials
   │
   ▼
[Authorization Middleware]  ←── Enforces policies
   │
   ▼
Endpoint Routing
   │
   ▼
Endpoint Execution
```

### Program.cs Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add authentication services
builder.Services.AddBasicAuthentication(builder.Configuration);

// Add authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireFeedAuth", policy =>
        policy.RequireClaim("AuthDomain", "feed", "admin"));
    
    options.AddPolicy("RequireAdminAuth", policy =>
        policy.RequireClaim("AuthDomain", "admin"));
});

var app = builder.Build();

// Configure middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseExceptionHandler("/error");

// Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
app.MapFeedEndpoints();      // /feed/*, /feeds/*
app.MapAdminEndpoints();     // /api/*
app.MapHealthEndpoint();     // /api/v1/health (public)

app.Run();
```

### Endpoint Authorization

```csharp
public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapFeedEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var feedGroup = endpoints.MapGroup("/feed")
            .RequireAuthorization("RequireFeedAuth");
        
        feedGroup.MapGet("/{channelId}/audio.rss", async (
            string channelId,
            FeedGenerator feedGenerator) =>
        {
            var feed = await feedGenerator.GenerateRssAsync(channelId, "audio");
            return Results.Text(feed, "application/rss+xml");
        });
        
        feedGroup.MapGet("/{channelId}/video.rss", async (
            string channelId,
            FeedGenerator feedGenerator) =>
        {
            var feed = await feedGenerator.GenerateRssAsync(channelId, "video");
            return Results.Text(feed, "application/rss+xml");
        });
        
        return endpoints;
    }
    
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var apiGroup = endpoints.MapGroup("/api/v1")
            .RequireAuthorization("RequireAdminAuth");
        
        apiGroup.MapGet("/channels", ChannelEndpoints.ListChannels);
        apiGroup.MapPost("/channels", ChannelEndpoints.CreateChannel);
        apiGroup.MapGet("/channels/{id}", ChannelEndpoints.GetChannel);
        apiGroup.MapPut("/channels/{id}", ChannelEndpoints.UpdateChannel);
        apiGroup.MapDelete("/channels/{id}", ChannelEndpoints.DeleteChannel);
        
        // Additional endpoints...
        
        return endpoints;
    }
    
    public static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/health", () =>
        {
            return Results.Ok(new
            {
                status = "healthy",
                version = "1.0.0",
                timestamp = DateTime.UtcNow
            });
        }).AllowAnonymous();
        
        return endpoints;
    }
}
```

### Authentication Headers

Successful authentication responses include:

```http
HTTP/1.1 200 OK
Content-Type: application/rss+xml
X-Auth-Domain: feed
```

Failed authentication responses:

```http
HTTP/1.1 401 Unauthorized
WWW-Authenticate: Basic realm="Yallarhorn Feeds", charset="UTF-8"
Content-Type: application/json

{
  "error": {
    "code": "UNAUTHORIZED",
    "message": "Authentication required",
    "details": "Include valid credentials in the Authorization header"
  }
}
```

---

## Security Considerations

### Transport Security

#### HTTPS Requirement

HTTP Basic Auth transmits credentials encoded (not encrypted) in each request. **HTTPS is mandatory in production.**

```csharp
// Program.cs - Enforce HTTPS in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}
```

#### Configuration

```yaml
server:
  host: "0.0.0.0"
  port: 8080
  # TLS configuration (if not using reverse proxy)
  tls:
    enabled: true
    cert_path: "/etc/ssl/certs/yallarhorn.crt"
    key_path: "/etc/ssl/private/yallarhorn.key"
```

### Credential Protection

#### In Configuration Files

1. **Never commit plaintext passwords**: Use `password_hash` or environment variables
2. **File permissions**: Restrict config file to owner read only

```bash
chmod 600 yallarhorn.yaml
chown yallarhorn:yallarhorn yallarhorn.yaml
```

#### In Memory

- Credentials are loaded once at startup and stored in memory
- Avoid logging credential values (redact in logs)

```csharp
_logger.LogInformation(
    "Validating credentials for user {Username} in domain {Domain}",
    username, authDomain);  // Never log password
```

#### In Transit

- All requests with credentials must use HTTPS
- Consider mutual TLS (mTLS) for enhanced security

### Timing Attack Mitigation

BCrypt includes built-in timing attack resistance through its constant-time comparison. Always use the library's verify function:

```csharp
// GOOD: Constant-time comparison (BCrypt)
bool isValid = BCrypt.Net.BCrypt.Verify(password, storedHash);

// BAD: Vulnerable to timing attacks
bool isValid = (password == storedPassword);
bool isValid = string.Equals(password, storedPassword);
```

### Rate Limiting

Combine authentication with rate limiting to prevent brute-force attacks:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("AuthRateLimit", context =>
    {
        var username = context.User.Identity?.Name ?? "anonymous";
        return RateLimitPartition.GetSlidingWindowLimiter(
            username,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4
            });
    });
});

// Apply to authenticated endpoints
app.MapAdminEndpoints().RequireRateLimiting("AuthRateLimit");
```

### Logging and Auditing

Log authentication events for security monitoring:

```csharp
public class AuditingLogger
{
    public void LogAuthenticationAttempt(string username, string authDomain, bool success, string clientIp)
    {
        if (success)
        {
            _logger.LogInformation(
                "Successful authentication: user={User}, domain={Domain}, ip={IP}",
                username, authDomain, clientIp);
        }
        else
        {
            _logger.LogWarning(
                "Failed authentication attempt: user={User}, domain={Domain}, ip={IP}",
                username, authDomain, clientIp);
        }
    }
}
```

### Security Checklist

| Item | Status | Notes |
|------|--------|-------|
| HTTPS enforced | Required | Non-negotiable for Basic Auth |
| Passwords hashed | Recommended | BCrypt with work factor ≥ 11 |
| Separate credentials | Required | feed_auth ≠ admin_auth |
| Rate limiting | Recommended | Prevent brute-force attacks |
| Audit logging | Recommended | Track auth successes/failures |
| Config file permissions | Required | 600, owner-only |
| Health endpoint public | Acceptable | No sensitive data exposed |
| Credential rotation | Recommended | Quarterly minimum |

---

## Configuration Reference

### Complete Authentication Configuration

```yaml
auth:
  # Feed authentication for podcast clients
  feed_credentials:
    enabled: true                          # Enable HTTP Basic Auth for feeds
    username: "feed-user"                  # Feed access username
    password: "${FEED_PASSWORD}"           # Password (env var recommended)
    password_hash: ""                      # OR: BCrypt hash (takes precedence)
    realm: "Yallarhorn Feeds"              # HTTP auth realm
  
  # Admin authentication for management API
  admin_auth:
    enabled: true                          # Enable HTTP Basic Auth for API
    username: "admin"                      # Admin username
    password: "${ADMIN_PASSWORD}"          # Password (env var recommended)
    password_hash: ""                      # OR: BCrypt hash (takes precedence)
    realm: "Yallarhorn Admin"              # HTTP auth realm
```

### Configuration Validation

On startup, Yallarhorn validates authentication configuration:

```
Configuration validation failed:
  - auth.feed_credentials.password: Password is required when feed_credentials is enabled
  - auth.admin_auth.password_hash: Invalid BCrypt hash format
```

### CLI Commands

```bash
# Generate a BCrypt password hash
yallarhorn auth hash-password
# Output: $2a$11$N9qo8uLOickgx2ZMRZoMy...

# Validate configuration
yallarhorn config validate

# Test authentication
curl -u admin:password http://localhost:8080/api/v1/status
```

---

## See Also

- [API Specification](./api-specification.md) - REST API endpoint documentation
- [Configuration Schema](./configuration.md) - Complete configuration reference
- [Feed Generation](./feed-generation.md) - RSS/Atom feed design

## References

- [RFC 7617: The 'Basic' HTTP Authentication Scheme](https://tools.ietf.org/html/rfc7617)
- [RFC 2617: HTTP Authentication](https://tools.ietf.org/html/rfc2617)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [BCrypt Algorithm](https://en.wikipedia.org/wiki/Bcrypt)