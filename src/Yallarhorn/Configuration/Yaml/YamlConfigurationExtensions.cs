using Microsoft.Extensions.Configuration;
using Yallarhorn.Configuration.Yaml;

namespace Yallarhorn.Configuration.Yaml;

/// <summary>
/// Extension methods for adding YAML configuration.
/// </summary>
public static class YamlConfigurationExtensions
{
    /// <summary>
    /// Adds a YAML configuration file.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="path">The path to the YAML file.</param>
    /// <param name="optional">Whether the file is optional.</param>
    /// <param name="expandEnvironmentVariables">Whether to expand environment variables.</param>
    /// <returns>The configuration builder.</returns>
    public static IConfigurationBuilder AddYamlFile(
        this IConfigurationBuilder builder,
        string path,
        bool optional = true,
        bool expandEnvironmentVariables = true)
    {
        return builder.AddYamlFile(s =>
        {
            s.FilePaths.Add(path);
            s.Optional = optional;
            s.ExpandEnvironmentVariables = expandEnvironmentVariables;
        });
    }

    /// <summary>
    /// Adds multiple YAML configuration files.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="paths">The paths to the YAML files.</param>
    /// <param name="optional">Whether the files are optional.</param>
    /// <param name="expandEnvironmentVariables">Whether to expand environment variables.</param>
    /// <returns>The configuration builder.</returns>
    public static IConfigurationBuilder AddYamlFiles(
        this IConfigurationBuilder builder,
        IEnumerable<string> paths,
        bool optional = true,
        bool expandEnvironmentVariables = true)
    {
        return builder.AddYamlFile(s =>
        {
            foreach (var path in paths)
            {
                s.FilePaths.Add(path);
            }
            s.Optional = optional;
            s.ExpandEnvironmentVariables = expandEnvironmentVariables;
        });
    }

    /// <summary>
    /// Adds a YAML configuration source with configuration.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The configuration builder.</returns>
    public static IConfigurationBuilder AddYamlFile(
        this IConfigurationBuilder builder,
        Action<YamlConfigurationSource> configure)
    {
        var source = new YamlConfigurationSource();
        configure(source);
        builder.Add(source);
        return builder;
    }

    /// <summary>
    /// Adds YAML configuration files with standard search order.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="baseName">The base name of the configuration file (default: "config").</param>
    /// <param name="environmentName">The environment name (e.g., "Development", "Production").</param>
    /// <returns>The configuration builder.</returns>
    public static IConfigurationBuilder AddYamlConfiguration(
        this IConfigurationBuilder builder,
        string baseName = "config",
        string? environmentName = null)
    {
        var files = new List<string>
        {
            $"{baseName}.yaml",
            $"{baseName}.yml"
        };

        if (!string.IsNullOrEmpty(environmentName))
        {
            files.Add($"{baseName}.{environmentName}.yaml");
            files.Add($"{baseName}.{environmentName}.yml");
        }

        builder.AddYamlFiles(files, optional: true, expandEnvironmentVariables: true);

        return builder;
    }
}