namespace Yallarhorn.CLI.Commands;

using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using Yallarhorn.Configuration;
using Yallarhorn.Configuration.Validation;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Lints configuration files for best practices and potential issues.
/// </summary>
public class ConfigLintCommand : ICommand
{
    private readonly ILogger<ConfigLintCommand> _logger;

    /// <inheritdoc/>
    public string Name => "lint";

    /// <inheritdoc/>
    public string Description => "Check configuration for best practices and potential issues";

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigLintCommand"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ConfigLintCommand(ILogger<ConfigLintCommand> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes the lint command.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 for success, non-zero for failure).</returns>
    public async Task<int> ExecuteAsync(string configPath, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(new[] { "--config", configPath }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        string? configPath = null;

        // Parse arguments
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--config" || args[i] == "-c") && i + 1 < args.Length)
            {
                configPath = args[i + 1];
                i++;
            }
        }

        // Default config path
        configPath ??= "./config.yaml";

        _logger.LogInformation("Linting configuration file: {ConfigPath}", configPath);

        if (!File.Exists(configPath))
        {
            _logger.LogError("Configuration file not found: {ConfigPath}", configPath);
            Console.Error.WriteLine($"Error: Configuration file not found: {configPath}");
            return 1;
        }

        try
        {
            var yaml = await File.ReadAllTextAsync(configPath, cancellationToken);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var options = deserializer.Deserialize<YallarhornOptions>(yaml);

            if (options == null)
            {
                _logger.LogError("Configuration file is empty or invalid YAML");
                Console.Error.WriteLine("Error: Configuration file is empty or contains invalid YAML");
                return 1;
            }

            var warnings = new List<string>();
            var errors = new List<string>();

            // Run validation first
            try
            {
                options.ValidateAndThrow();
                var validator = new YallarhornOptionsValidator();
                validator.ValidateAndThrow(options);
                _logger.LogInformation("Configuration validation passed");
                Console.WriteLine("✓ Configuration is valid");
            }
            catch (ValidationException ex)
            {
                errors.Add(ex.Message);
                _logger.LogError("Configuration validation failed: {Message}", ex.Message);
            }

            // Lint checks
            RunLintChecks(options, warnings);

            // Output results
            if (warnings.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Warnings:");
                foreach (var warning in warnings)
                {
                    Console.WriteLine($"  ⚠ {warning}");
                    _logger.LogWarning("{Warning}", warning);
                }
            }

            if (errors.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Errors:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  ✗ {error}");
                }
                return 1;
            }

            if (warnings.Count == 0 && errors.Count == 0)
            {
                Console.WriteLine("✓ No warnings or errors found");
            }

            Console.WriteLine();
            Console.WriteLine($"Lint complete: {errors.Count} error(s), {warnings.Count} warning(s)");
            return errors.Count > 0 ? 1 : 0;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            _logger.LogError(ex, "Failed to parse YAML configuration");
            Console.Error.WriteLine($"YAML parsing error at line {ex.Start.Line}, column {ex.Start.Column}:");
            Console.Error.WriteLine($"  {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error linting configuration");
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private void RunLintChecks(YallarhornOptions options, List<string> warnings)
    {
        _logger.LogInformation("Running lint checks");

        // Check poll interval
        if (options.PollInterval < 600)
        {
            warnings.Add($"Poll interval ({options.PollInterval}s) is less than recommended (600s). This may cause excessive API calls.");
        }

        // Check download directory
        if (options.DownloadDir.StartsWith("/tmp") || options.DownloadDir.StartsWith("/temp"))
        {
            warnings.Add($"Download directory '{options.DownloadDir}' is in a temporary location. Downloads may be lost on restart.");
        }

        // Check temp directory
        if (options.TempDir.StartsWith("/tmp") || options.TempDir.StartsWith("/temp"))
        {
            warnings.Add($"Temp directory '{options.TempDir}' is in a temporary location.");
        }

        // Check concurrent downloads
        if (options.MaxConcurrentDownloads > 5)
        {
            warnings.Add($"Max concurrent downloads ({options.MaxConcurrentDownloads}) is high. This may impact system performance.");
        }

        // Check for plaintext passwords in auth
        if (options.Auth.FeedCredentials.Enabled)
        {
            if (!string.IsNullOrEmpty(options.Auth.FeedCredentials.Password))
            {
                var password = options.Auth.FeedCredentials.Password;
                if (!IsBCryptHash(password))
                {
                    warnings.Add("Feed credentials password appears to be plaintext. Consider using 'auth hash-password' to generate a secure hash.");
                }
            }
            _logger.LogInformation("Feed credentials enabled");
        }

        if (options.Auth.AdminAuth.Enabled)
        {
            if (!string.IsNullOrEmpty(options.Auth.AdminAuth.Password))
            {
                var password = options.Auth.AdminAuth.Password;
                if (!IsBCryptHash(password))
                {
                    warnings.Add("Admin auth password appears to be plaintext. Consider using 'auth hash-password' to generate a secure hash.");
                }
            }
            _logger.LogInformation("Admin auth enabled");
        }

        // Check channels
        foreach (var channel in options.Channels)
        {
            if (string.IsNullOrEmpty(channel.Name))
            {
                warnings.Add("Channel has no name configured");
            }

            if (string.IsNullOrEmpty(channel.Url))
            {
                warnings.Add($"Channel '{channel.Name}' has no URL configured");
            }

            if (!channel.Enabled)
            {
                _logger.LogInformation("Channel '{Name}' is disabled", channel.Name);
            }
        }

        // Check database path
        if (string.IsNullOrEmpty(options.Database.Path))
        {
            warnings.Add("Database path is not configured");
        }

        // Check server port
        if (options.Server.Port < 1024)
        {
            warnings.Add($"Server port ({options.Server.Port}) is a privileged port. Application may need elevated permissions.");
        }

        _logger.LogInformation("Lint checks completed with {Count} warnings", warnings.Count);
    }

    private static bool IsBCryptHash(string value)
    {
        // BCrypt hashes start with $2a$, $2b$, or $2y$
        return value.StartsWith("$2a$") || value.StartsWith("$2b$") || value.StartsWith("$2y$");
    }
}