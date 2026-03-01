using System.Reflection;

namespace Yallarhorn.Services;

/// <summary>
/// Implementation of <see cref="IVersionService"/> that reads version information from assembly attributes.
/// </summary>
public class VersionService : IVersionService
{
    private readonly string? _informationalVersion;
    private readonly Version _assemblyVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionService"/> class.
    /// </summary>
    public VersionService()
    {
        var assembly = Assembly.GetExecutingAssembly();
        
        _informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        
        _assemblyVersion = assembly.GetName().Version ?? new Version(0, 0, 0, 0);
    }

    /// <inheritdoc />
    public string GetVersion()
    {
        return _informationalVersion ?? _assemblyVersion.ToString();
    }

    /// <inheritdoc />
    public string GetVersionWithoutPrerelease()
    {
        // InformationalVersion may contain prerelease suffix like "1.0.0-rc1"
        // Strip everything after '-' if present
        var version = _informationalVersion ?? _assemblyVersion.ToString();
        var dashIndex = version.IndexOf('-');
        return dashIndex >= 0 ? version.Substring(0, dashIndex) : version;
    }

    /// <inheritdoc />
    public Version GetAssemblyVersion()
    {
        return _assemblyVersion;
    }
}