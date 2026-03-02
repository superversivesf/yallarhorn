using Scalar.AspNetCore;
using Yallarhorn.CLI;
using Yallarhorn.Configuration;
using Yallarhorn.Configuration.Yaml;
using Yallarhorn.Extensions;
using Yallarhorn.Logging;
using Yallarhorn.Services;

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
var configFound = false;

if (!string.IsNullOrEmpty(configPath))
{
    configFound = File.Exists(configPath);
}
else
{
    // Auto-detect config file
    if (File.Exists("/app/yallarhorn.yaml"))
    {
        configPath = "/app/yallarhorn.yaml";
        configFound = true;
    }
    else if (File.Exists("yallarhorn.yaml"))
    {
        configPath = "yallarhorn.yaml";
        configFound = true;
    }
}

if (configFound && !string.IsNullOrEmpty(configPath))
{
    // For absolute paths, read the file directly without using FileProvider
    // The built-in FileProvider is rooted at content root and can't handle absolute paths
    if (Path.IsPathRooted(configPath))
    {
        Console.WriteLine($"[DEBUG] Loading YAML from absolute path: {configPath}");
        var yamlContent = File.ReadAllText(configPath);
        Console.WriteLine($"[DEBUG] YAML content length: {yamlContent.Length} chars");
        
        // Parse YAML directly and add as in-memory collection
        var yaml = new YamlDotNet.RepresentationModel.YamlStream();
        yaml.Load(new StringReader(yamlContent));
        
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (yaml.Documents.Count > 0 && yaml.Documents[0].RootNode is YamlDotNet.RepresentationModel.YamlMappingNode mapping)
        {
            FlattenYaml(mapping, string.Empty, data);
        }
        
        Console.WriteLine($"[DEBUG] Parsed {data.Count} keys from YAML:");
        foreach (var kv in data.Take(10))
        {
            Console.WriteLine($"  {kv.Key} = {kv.Value}");
        }
        
        builder.Configuration.AddInMemoryCollection(data);
    }
    else
    {
        builder.Configuration.AddYamlFile(configPath, optional: true);
    }
}
else
{
    // Log warning - no configuration file found but app can still start
    Console.WriteLine("Warning: No configuration file found. Using defaults.");
    Console.WriteLine("  Searched: --config argument, /app/yallarhorn.yaml, ./yallarhorn.yaml");
}

// Debug: show all Server:* config keys before binding
var allServerKeys = builder.Configuration.AsEnumerable()
    .Where(kv => kv.Key.StartsWith("Server:", StringComparison.OrdinalIgnoreCase) && kv.Value != null)
    .ToList();
Console.WriteLine($"[DEBUG] Server keys from config (count={allServerKeys.Count}):");
foreach (var kv in allServerKeys.OrderBy(x => x.Key))
{
    Console.WriteLine($"  {kv.Key} = {kv.Value}");
}

// Debug: show ALL keys to see if YAML keys are different
var allKeys = builder.Configuration.AsEnumerable()
    .Where(kv => kv.Value != null)
    .OrderBy(x => x.Key)
    .ToList();
Console.WriteLine($"[DEBUG] ALL config keys (count={allKeys.Count}):");
foreach (var kv in allKeys.Take(50)) // First 50 to avoid spam
{
    Console.WriteLine($"  {kv.Key} = {kv.Value}");
}
if (allKeys.Count > 50)
{
    Console.WriteLine($"  ... and {allKeys.Count - 50} more keys");
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

// Log configuration for debugging
Console.WriteLine($"Configuration loaded:");
Console.WriteLine($"  Server.BaseUrl: {serverOptions.BaseUrl}");
Console.WriteLine($"  Server.Port: {serverOptions.Port}");
Console.WriteLine($"  Server.FeedPath: {serverOptions.FeedPath}");
Console.WriteLine($"  Config file: {configPath ?? "none"}");

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

// Version endpoint
app.MapGet("/version", (IVersionService versionService) => Results.Ok(new 
{ 
    version = versionService.GetVersion(), 
    framework = "net10.0",
    timestamp = DateTime.UtcNow 
}))
   .WithName("Version");

app.MapGet("/", () => Results.Redirect("/scalar/v1"))
   .WithName("Home");

app.Run();

// Helper method to flatten YAML into config keys
static void FlattenYaml(YamlDotNet.RepresentationModel.YamlMappingNode node, string prefix, Dictionary<string, string?> data)
{
    foreach (var entry in node.Children)
    {
        var key = entry.Key.ToString() ?? "";
        var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}:{key}";

        switch (entry.Value)
        {
            case YamlDotNet.RepresentationModel.YamlMappingNode mapping:
                FlattenYaml(mapping, fullKey, data);
                break;
            case YamlDotNet.RepresentationModel.YamlSequenceNode sequence:
                for (var i = 0; i < sequence.Children.Count; i++)
                {
                    var itemKey = $"{fullKey}:{i}";
                    var item = sequence.Children[i];
                    switch (item)
                    {
                        case YamlDotNet.RepresentationModel.YamlMappingNode itemMapping:
                            FlattenYaml(itemMapping, itemKey, data);
                            break;
                        case YamlDotNet.RepresentationModel.YamlScalarNode itemScalar:
                            data[itemKey] = itemScalar.Value;
                            break;
                        default:
                            data[itemKey] = item?.ToString();
                            break;
                    }
                }
                break;
            case YamlDotNet.RepresentationModel.YamlScalarNode scalar:
                data[fullKey] = scalar.Value;
                break;
            default:
                data[fullKey] = entry.Value?.ToString();
                break;
        }
    }
}

// Make Program accessible for integration tests
/// <summary>
/// The main application entry point. Exposed for integration testing.
/// </summary>
public partial class Program { }