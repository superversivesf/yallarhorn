namespace Yallarhorn.CLI;

using System.CommandLine;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles command line argument parsing and command routing.
/// </summary>
public class CommandLineHandler
{
    private readonly ILogger<CommandLineHandler> _logger;
    private readonly IServiceProvider? _serviceProvider;
    private readonly RootCommand _rootCommand;
    private readonly Option<string?> _configOption;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandLineHandler"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    public CommandLineHandler(ILogger<CommandLineHandler> logger, IServiceProvider? serviceProvider = null)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        
        _configOption = new Option<string?>("--config", "-c")
        {
            Description = "Path to the configuration file"
        };
        
        _rootCommand = BuildRootCommand();
    }

    private RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("Yallarhorn - YouTube podcast feed generator");
        rootCommand.Options.Add(_configOption);

        // Add config command
        var configCommand = new Command("config", "Configuration management commands");
        
        var validateCommand = new Command("validate", "Validate the configuration file");
        validateCommand.Options.Add(_configOption);
        validateCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var configPath = parseResult.GetValue(_configOption);
            var exitCode = await ExecuteConfigValidateAsync(configPath, cancellationToken);
            return exitCode;
        });
        configCommand.Subcommands.Add(validateCommand);

        var lintCommand = new Command("lint", "Lint the configuration file for best practices");
        lintCommand.Options.Add(_configOption);
        lintCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var configPath = parseResult.GetValue(_configOption);
            var exitCode = await ExecuteConfigLintAsync(configPath, cancellationToken);
            return exitCode;
        });
        configCommand.Subcommands.Add(lintCommand);

        rootCommand.Subcommands.Add(configCommand);

        // Add auth command
        var authCommand = new Command("auth", "Authentication management commands");
        
        var hashPasswordCommand = new Command("hash-password", "Generate a BCrypt hash for a password");
        var passwordOption = new Option<string?>("--password", "-p")
        {
            Description = "The password to hash (will prompt if not provided)"
        };
        hashPasswordCommand.Options.Add(passwordOption);
        hashPasswordCommand.Options.Add(_configOption);
        hashPasswordCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var password = parseResult.GetValue(passwordOption);
            var args = new List<string>();
            if (!string.IsNullOrEmpty(password))
            {
                args.Add("--password");
                args.Add(password);
            }
            var exitCode = await ExecuteAuthHashPasswordAsync(args.ToArray(), cancellationToken);
            return exitCode;
        });
        authCommand.Subcommands.Add(hashPasswordCommand);

        rootCommand.Subcommands.Add(authCommand);

        // Add run command (explicit, but also the default)
        var runCommand = new Command("run", "Run the server (default behavior)");
        runCommand.Options.Add(_configOption);
        rootCommand.Subcommands.Add(runCommand);

        return rootCommand;
    }

    /// <summary>
    /// Parses command line arguments into options.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Parsed options, or null if should run server.</returns>
    public CommandLineOptions? Parse(string[] args)
    {
        _logger.LogDebug("Parsing command line arguments: {Args}", string.Join(" ", args));

        // Handle empty args - run server
        if (args.Length == 0)
        {
            _logger.LogInformation("No arguments provided, running server");
            return null;
        }

        // Handle help
        if (args.Contains("--help") || args.Contains("-h") || args.Contains("/?"))
        {
            return new CommandLineOptions { HelpRequested = true };
        }

        // Handle version
        if (args.Contains("--version"))
        {
            return new CommandLineOptions { VersionRequested = true };
        }

        var options = new CommandLineOptions();
        var remainingArgs = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--config":
                case "-c":
                    if (i + 1 < args.Length)
                    {
                        options.ConfigPath = args[i + 1];
                        i++; // Skip the next argument
                    }
                    break;

                case "config":
                    options.Command = "config";
                    if (i + 1 < args.Length)
                    {
                        options.SubCommand = args[i + 1];
                        i++; // Skip the next argument
                    }
                    break;

                case "auth":
                    options.Command = "auth";
                    if (i + 1 < args.Length)
                    {
                        options.SubCommand = args[i + 1];
                        i++; // Skip the next argument
                    }
                    break;

                case "run":
                    // Explicit run command
                    return null;

                default:
                    // Collect remaining args (like --password for auth commands)
                    remainingArgs.Add(args[i]);
                    break;
            }
        }

        options.RemainingArgs = remainingArgs.ToArray();

        // If no command specified AND no config path, run server
        // If config path is specified without command, return options (for --config flag parsing)
        if (string.IsNullOrEmpty(options.Command) && string.IsNullOrEmpty(options.ConfigPath))
        {
            return null;
        }

        return options;
    }

    /// <summary>
    /// Executes the command specified in options.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Exit code.</returns>
    public async Task<int> ExecuteAsync(string[] args)
    {
        _logger.LogInformation("Executing CLI command");
        return await Task.FromResult(_rootCommand.Parse(args).Invoke());
    }

    private async Task<int> ExecuteConfigValidateAsync(string? configPath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing config validate command");
        
        Commands.ConfigValidateCommand? command = null;
        if (_serviceProvider != null)
        {
            command = _serviceProvider.GetService(typeof(Commands.ConfigValidateCommand)) as Commands.ConfigValidateCommand;
        }
        
        if (command == null)
        {
            // Create without DI if not registered
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Commands.ConfigValidateCommand>();
            command = new Commands.ConfigValidateCommand(logger);
        }

        var args = configPath != null ? new[] { "--config", configPath } : Array.Empty<string>();
        return await command.ExecuteAsync(args, cancellationToken);
    }

    private async Task<int> ExecuteConfigLintAsync(string? configPath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing config lint command");
        
        Commands.ConfigLintCommand? command = null;
        if (_serviceProvider != null)
        {
            command = _serviceProvider.GetService(typeof(Commands.ConfigLintCommand)) as Commands.ConfigLintCommand;
        }
        
        if (command == null)
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Commands.ConfigLintCommand>();
            command = new Commands.ConfigLintCommand(logger);
        }

        var args = configPath != null ? new[] { "--config", configPath } : Array.Empty<string>();
        return await command.ExecuteAsync(args, cancellationToken);
    }

    private async Task<int> ExecuteAuthHashPasswordAsync(string[] args, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing auth hash-password command");
        
        Commands.AuthHashPasswordCommand? command = null;
        if (_serviceProvider != null)
        {
            command = _serviceProvider.GetService(typeof(Commands.AuthHashPasswordCommand)) as Commands.AuthHashPasswordCommand;
        }
        
        if (command == null)
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Commands.AuthHashPasswordCommand>();
            command = new Commands.AuthHashPasswordCommand(logger);
        }

        return await command.ExecuteAsync(args, cancellationToken);
    }
}