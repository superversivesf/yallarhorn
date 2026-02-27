namespace Yallarhorn.CLI.Commands;

using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using Yallarhorn.Configuration;
using Yallarhorn.Configuration.Validation;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Validates configuration files.
/// </summary>
public class ConfigValidateCommand : ICommand
{
    private readonly ILogger<ConfigValidateCommand> _logger;

    /// <inheritdoc/>
    public string Name => "validate";

    /// <inheritdoc/>
    public string Description => "Validate the configuration file for errors";

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigValidateCommand"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ConfigValidateCommand(ILogger<ConfigValidateCommand> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes the validation command.
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

        _logger.LogInformation("Validating configuration file: {ConfigPath}", configPath);

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

            // Run validation
            options.ValidateAndThrow();

            // Also run FluentValidation
            var validator = new YallarhornOptionsValidator();
            validator.ValidateAndThrow(options);

            _logger.LogInformation("Configuration is valid");
            Console.WriteLine($"Configuration '{configPath}' is valid.");
            return 0;
        }
        catch (ValidationException ex)
        {
            _logger.LogError(ex, "Configuration validation failed");
            Console.Error.WriteLine($"Configuration validation failed:");
            Console.Error.WriteLine($"  {ex.Message}");
            return 1;
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
            _logger.LogError(ex, "Unexpected error validating configuration");
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}