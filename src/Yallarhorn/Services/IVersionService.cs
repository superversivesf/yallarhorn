namespace Yallarhorn.Services;

/// <summary>
/// Service for retrieving version information about the application.
/// </summary>
public interface IVersionService
{
    /// <summary>
    /// Gets the full version string including prerelease suffix (e.g., "1.0.0-rc1").
    /// </summary>
    /// <returns>The full semantic version string.</returns>
    string GetVersion();

    /// <summary>
    /// Gets the version string without the prerelease suffix (e.g., "1.0.0").
    /// </summary>
    /// <returns>The version string without prerelease information.</returns>
    string GetVersionWithoutPrerelease();

    /// <summary>
    /// Gets the assembly version as a Version object.
    /// </summary>
    /// <returns>The assembly version.</returns>
    Version GetAssemblyVersion();
}