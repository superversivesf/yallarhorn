namespace Yallarhorn.CLI.Commands;

using Microsoft.Extensions.Logging;

/// <summary>
/// Generates BCrypt hashes for authentication passwords.
/// </summary>
public class AuthHashPasswordCommand : ICommand
{
    private readonly ILogger<AuthHashPasswordCommand> _logger;

    /// <inheritdoc/>
    public string Name => "hash-password";

    /// <inheritdoc/>
    public string Description => "Generate a BCrypt hash for a password to use in configuration";

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthHashPasswordCommand"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public AuthHashPasswordCommand(ILogger<AuthHashPasswordCommand> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        string? password = null;

        // Parse arguments
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--password" || args[i] == "-p") && i + 1 < args.Length)
            {
                password = args[i + 1];
                i++;
            }
        }

        // Check for interactive mode (prompt for password)
        if (password == null)
        {
            // Try to find just the flag without value (interactive mode)
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--password" || args[i] == "-p")
                {
                    password = PromptForPassword();
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(password))
        {
            _logger.LogError("Password is required");
            Console.Error.WriteLine("Error: Password is required.");
            Console.Error.WriteLine("Usage: auth hash-password --password <password>");
            Console.Error.WriteLine("       auth hash-password -p <password>");
            Console.Error.WriteLine("       auth hash-password (interactive mode)");
            return Task.FromResult(1);
        }

        try
        {
            _logger.LogInformation("Generating BCrypt hash for password");
            
            // Generate BCrypt hash with default work factor (12)
            var hash = BCrypt.Net.BCrypt.HashPassword(password);

            _logger.LogInformation("Password hash generated successfully");

            // Output only the hash to stdout (easy to capture)
            Console.WriteLine(hash);

            // Print helpful info to stderr
            Console.Error.WriteLine();
            Console.Error.WriteLine("Password hash generated successfully.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Add this to your configuration:");
            Console.Error.WriteLine();
            Console.Error.WriteLine("  auth:");
            Console.Error.WriteLine("    feedCredentials:");
            Console.Error.WriteLine("      password: <hash>");
            Console.Error.WriteLine("    adminAuth:");
            Console.Error.WriteLine("      password: <hash>");
            Console.Error.WriteLine();

            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate password hash");
            Console.Error.WriteLine($"Error: Failed to generate password hash: {ex.Message}");
            return Task.FromResult(1);
        }
    }

    private static string? PromptForPassword()
    {
        Console.Error.Write("Enter password: ");
        
        // Note: In a production CLI, we'd use Console.ReadKey with echo disabled
        // For simplicity, we read the line. In real implementation, consider using
        // a library like Spectre.Console for secure password input.
        var password = Console.ReadLine();
        
        if (string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine("Password cannot be empty.");
            return null;
        }

        Console.Error.Write("Confirm password: ");
        var confirm = Console.ReadLine();

        if (password != confirm)
        {
            Console.Error.WriteLine("Passwords do not match.");
            return null;
        }

        return password;
    }
}