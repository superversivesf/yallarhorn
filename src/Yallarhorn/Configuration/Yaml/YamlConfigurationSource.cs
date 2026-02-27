using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace Yallarhorn.Configuration.Yaml;

/// <summary>
/// Configuration source for YAML files.
/// </summary>
public class YamlConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// Gets or sets the file paths to load (in order of precedence).
    /// </summary>
    public IList<string> FilePaths { get; } = new List<string>();

    /// <summary>
    /// Gets or sets the file provider.
    /// </summary>
    public IFileProvider? FileProvider { get; set; }

    /// <summary>
    /// Gets or sets whether the file is optional.
    /// </summary>
    public bool Optional { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to expand environment variables.
    /// </summary>
    public bool ExpandEnvironmentVariables { get; set; } = true;

    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        FileProvider ??= builder.GetFileProvider();
        return new YamlConfigurationProvider(this);
    }
}