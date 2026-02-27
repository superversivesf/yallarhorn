namespace Yallarhorn.CLI.Commands;

/// <summary>
/// Interface for CLI commands.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Gets the name of the command.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the description of the command.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes the command asynchronously.
    /// </summary>
    /// <param name="args">Command arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 for success, non-zero for failure).</returns>
    Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default);
}