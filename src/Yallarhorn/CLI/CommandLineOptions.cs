namespace Yallarhorn.CLI;

/// <summary>
/// Represents parsed command line options for the Yallarhorn CLI.
/// </summary>
public class CommandLineOptions
{
    /// <summary>
    /// Gets or sets the path to the custom configuration file.
    /// </summary>
    public string? ConfigPath { get; set; }

    /// <summary>
    /// Gets or sets the command to execute (e.g., "config", "auth").
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// Gets or sets the subcommand to execute (e.g., "validate", "lint").
    /// </summary>
    public string? SubCommand { get; set; }

    /// <summary>
    /// Gets or sets remaining arguments passed to the command.
    /// </summary>
    public string[] RemainingArgs { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets a value indicating whether help was requested.
    /// </summary>
    public bool HelpRequested { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether version was requested.
    /// </summary>
    public bool VersionRequested { get; set; }
}