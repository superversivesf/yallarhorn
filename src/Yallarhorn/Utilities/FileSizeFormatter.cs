using System.Text;

namespace Yallarhorn.Utilities;

/// <summary>
/// Formats file sizes in bytes to human-readable format.
/// </summary>
public static class FileSizeFormatter
{
    private const long KB = 1024L;
    private const long MB = KB * 1024;
    private const long GB = MB * 1024;
    private const long TB = GB * 1024;
    private const long PB = TB * 1024;

    private static readonly string[] SizeUnits = ["B", "KB", "MB", "GB", "TB", "PB"];
    private static readonly string[] BinarySizeUnits = ["B", "KiB", "MiB", "GiB", "TiB", "PiB"];

    /// <summary>
    /// Formats a file size in bytes to a human-readable string using decimal units.
    /// </summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <param name="decimalPlaces">The number of decimal places to use (default: 1).</param>
    /// <returns>A formatted string like "1.5 MB" or "512 B".</returns>
    /// <exception cref="ArgumentException">Thrown when bytes is negative.</exception>
    public static string Format(long bytes, int decimalPlaces = 1)
    {
        if (bytes < 0)
        {
            throw new ArgumentException("File size cannot be negative.", nameof(bytes));
        }

        if (bytes < KB)
        {
            return $"{bytes} B";
        }

        var value = (double)bytes;
        var unitIndex = 0;

        while (value >= KB && unitIndex < SizeUnits.Length - 1)
        {
            value /= KB;
            unitIndex++;
        }

        return FormatWithDecimalPlaces(value, decimalPlaces, SizeUnits[unitIndex]);
    }

    /// <summary>
    /// Formats a file size in bytes to a human-readable string using binary units (KiB, MiB, etc.).
    /// </summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <param name="decimalPlaces">The number of decimal places to use (default: 1).</param>
    /// <returns>A formatted string like "1.5 MiB" or "512 B".</returns>
    /// <exception cref="ArgumentException">Thrown when bytes is negative.</exception>
    public static string FormatBinary(long bytes, int decimalPlaces = 1)
    {
        if (bytes < 0)
        {
            throw new ArgumentException("File size cannot be negative.", nameof(bytes));
        }

        if (bytes < KB)
        {
            return $"{bytes} B";
        }

        var value = (double)bytes;
        var unitIndex = 0;

        while (value >= KB && unitIndex < BinarySizeUnits.Length - 1)
        {
            value /= KB;
            unitIndex++;
        }

        return FormatWithDecimalPlaces(value, decimalPlaces, BinarySizeUnits[unitIndex]);
    }

    private static string FormatWithDecimalPlaces(double value, int decimalPlaces, string unit)
    {
        if (decimalPlaces <= 0)
        {
            return $"{Math.Round(value)} {unit}";
        }

        // Format with specified decimal places
        var formatted = value.ToString($"F{decimalPlaces}");
        
        // For default behavior (1 decimal), trim unnecessary trailing zeros
        // This gives "1 KB" instead of "1.0 KB" but "1.5 KB" for decimals
        if (decimalPlaces == 1)
        {
            formatted = formatted.TrimEnd('0').TrimEnd('.');
        }
        
        return $"{formatted} {unit}";
    }
}