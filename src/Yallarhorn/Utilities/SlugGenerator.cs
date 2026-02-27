using System.Text;
using System.Text.RegularExpressions;

namespace Yallarhorn.Utilities;

/// <summary>
/// Generates URL-safe slugs from text for channel/episode naming.
/// </summary>
public static class SlugGenerator
{
    private static readonly Regex NonAlphanumericPattern = new(@"[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex MultipleHyphensPattern = new(@"-+", RegexOptions.Compiled);
    private static readonly Regex LeadingTrailingHyphensPattern = new(@"^-|-$", RegexOptions.Compiled);

    /// <summary>
    /// Generates a URL-safe slug from the input text.
    /// </summary>
    /// <param name="text">The input text to convert.</param>
    /// <returns>A lowercase, hyphen-separated slug.</returns>
    public static string Generate(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // Convert to lowercase
        var slug = text.ToLowerInvariant();

        // Replace underscores with hyphens
        slug = slug.Replace('_', '-');

        // Replace all non-alphanumeric characters with hyphens
        slug = NonAlphanumericPattern.Replace(slug, "-");

        // Collapse multiple hyphens into one
        slug = MultipleHyphensPattern.Replace(slug, "-");

        // Remove leading and trailing hyphens
        slug = LeadingTrailingHyphensPattern.Replace(slug, string.Empty);

        return slug;
    }
}