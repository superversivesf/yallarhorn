using Scalar.AspNetCore;
using Yallarhorn.CLI;
using Yallarhorn.Configuration;
using Yallarhorn.Configuration.Yaml;
using Yallarhorn.Extensions;
using Yallarhorn.Logging;

// Parse command line arguments first
var argsToProcess = args;
var cliHandler = new CommandLineHandler(
    LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<CommandLineHandler>(),
    null! // Service provider not needed for basic parsing
);

var options = cliHandler.Parse(argsToProcess);

// Handle CLI commands
if (options != null)
{
    if (options.HelpRequested)
    {
        await cliHandler.ExecuteAsync(new[] { "--help" });
        return;
    }

    if (options.VersionRequested)
    {
        await cliHandler.ExecuteAsync(new[] { "--version" });
        return;
    }

    if (!string.IsNullOrEmpty(options.Command))
    {
        // Execute CLI command
        Environment.ExitCode = await cliHandler.ExecuteAsync(argsToProcess);
        return;
    }

    // If only --config was specified without a command, run server with custom config
    // This is handled below by the builder
}

var builder = WebApplication.CreateBuilder(argsToProcess);

// Load YAML config file
// Priority: 1. --config argument, 2. /app/yallarhorn.yaml (Docker), 3. ./yallarhorn.yaml (local)
var configPath = options?.ConfigPath;
if (string.IsNullOrEmpty(configPath))
{
    if (File.Exists("/app/yallarhorn.yaml"))
    {
        configPath = "/app/yallarhorn.yaml";
    }
    else if (File.Exists("yallarhorn.yaml"))
    {
        configPath = "yallarhorn.yaml";
    }
}

if (!string.IsNullOrEmpty(configPath))
{
    builder.Configuration.AddYamlFile(configPath, optional: false);
}

// Add Serilog logging
builder.AddYallarhornLogging();

// Add Yallarhorn services (DI, health checks, OpenAPI)
builder.AddYallarhornServices();

// Add core Yallarhorn services (database, repositories, domain services)
builder.Services.AddYallarhornCoreServices(builder.Configuration);

// Add rate limiting services
builder.Services.AddRateLimiterServices();

// Add background workers (conditional based on configuration)
builder.Services.AddYallarhornBackgroundWorkers(builder.Configuration);

// Configure Kestrel from ServerOptions
var serverOptions = builder.Configuration.GetSection(ServerOptions.SectionName).Get<ServerOptions>()
    ?? new ServerOptions();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(serverOptions.Port);
});

var app = builder.Build();

// Configure request pipeline
// Initialize database and seed channels from config
await app.UseYallarhornPipelineAsync();

// Serilog enrichers are configured in AddYallarhornLogging

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Only use HTTPS redirection in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Rate limiting enforcement
app.UseRateLimiter();

// Rate limit headers middleware
app.UseRateLimitHeaders();

// Health check endpoints
app.UseYallarhornHealthChecks();

// Map controller endpoints
app.MapControllers();

// Sample endpoint
app.MapGet("/health/simple", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithName("SimpleHealth");

app.MapGet("/", () => Results.Redirect("/scalar/v1"))
   .WithName("Home");

app.Run();

// Make Program accessible for integration tests
/// <summary>
/// The main application entry point. Exposed for integration testing.
/// </summary>
public partial class Program { }