using System.Text;

namespace Yallarhorn.Utilities;

/// <summary>
/// Expands environment variables in strings using ${VAR}, ${VAR:-default}, and ${VAR:?error} syntax.
/// </summary>
public static class EnvVarExpander
{

    /// <summary>
    /// Expands environment variables in the input string.
    /// </summary>
    /// <param name="input">The input string containing variable placeholders.</param>
    /// <returns>The input string with variables expanded to their values.</returns>
    /// <remarks>
    /// Supported syntaxes:
    /// - ${VAR} - Expands to the value of VAR, or empty string if not set.
    /// - ${VAR:-default} - Expands to the value of VAR, or 'default' if not set.
    /// - ${VAR:?error} - Expands to the value of VAR, throws InvalidOperationException with 'error' message if not set.
    /// </remarks>
    public static string? Expand(string? input)
    {
        return ExpandWithEnvironment(input, Environment.GetEnvironmentVariable);
    }

    /// <summary>
    /// Expands environment variables in the input string using a provided environment dictionary.
    /// </summary>
    /// <param name="input">The input string containing variable placeholders.</param>
    /// <param name="environment">A dictionary of environment variables.</param>
    /// <returns>The input string with variables expanded to their values.</returns>
    public static string? ExpandWithEnvironment(string? input, IReadOnlyDictionary<string, string> environment)
    {
        return ExpandWithEnvironment(input, name => environment.TryGetValue(name, out var value) ? value : null);
    }

    private static string? ExpandWithEnvironment(string? input, Func<string, string?> getenv)
    {
        if (input is null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var result = new StringBuilder();

        // Manual parsing for more control
        var i = 0;
        while (i < input.Length)
        {
            // Look for ${
            if (i < input.Length - 1 && input[i] == '$' && input[i + 1] == '{')
            {
                var endBrace = FindMatchingBrace(input, i + 2);
                if (endBrace == -1)
                {
                    // No matching brace, append rest and exit
                    result.Append(input.AsSpan(i));
                    break;
                }

                var variableExpression = input.AsSpan(i + 2, endBrace - i - 2);
                var expanded = ExpandVariable(variableExpression, getenv, input);
                result.Append(expanded);
                i = endBrace + 1;
            }
            else
            {
                result.Append(input[i]);
                i++;
            }
        }

        return result.ToString();
    }

    private static int FindMatchingBrace(string input, int start)
    {
        var depth = 1;
        for (var i = start; i < input.Length; i++)
        {
            if (input[i] == '{')
            {
                depth++;
            }
            else if (input[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private static string ExpandVariable(ReadOnlySpan<char> expression, Func<string, string?> getenv, string originalInput)
    {
        var expressionStr = expression.ToString();

        // Check for :- (default) syntax
        var defaultIndex = expressionStr.IndexOf(":-", StringComparison.Ordinal);
        if (defaultIndex > 0)
        {
            var varName = expressionStr[..defaultIndex];
            var defaultValue = expressionStr[(defaultIndex + 2)..];
            var value = getenv(varName);
            return value ?? defaultValue;
        }

        // Check for :? (error) syntax
        var errorIndex = expressionStr.IndexOf(":?", StringComparison.Ordinal);
        if (errorIndex > 0)
        {
            var varName = expressionStr[..errorIndex];
            var errorMessage = expressionStr[(errorIndex + 2)..];
            var value = getenv(varName);
            
            if (value is null)
            {
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(errorMessage)
                        ? $"Environment variable '{varName}' is required but not set."
                        : $"Environment variable '{varName}': {errorMessage}");
            }
            
            return value;
        }

        // Simple ${VAR} syntax
        return getenv(expressionStr) ?? string.Empty;
    }
}