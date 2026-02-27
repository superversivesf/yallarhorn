using System.Text;

namespace Yallarhorn.Utilities;

/// <summary>
/// Formats durations in seconds to human-readable HH:MM:SS format.
/// </summary>
public static class DurationFormatter
{
    private const int SecondsPerMinute = 60;
    private const int SecondsPerHour = 3600;

    /// <summary>
    /// Formats a duration in seconds to HH:MM:SS format.
    /// </summary>
    /// <param name="seconds">The duration in seconds.</param>
    /// <returns>A string in HH:MM:SS format.</returns>
    /// <exception cref="ArgumentException">Thrown when seconds is negative.</exception>
    public static string Format(long seconds)
    {
        if (seconds < 0)
        {
            throw new ArgumentException("Duration cannot be negative.", nameof(seconds));
        }

        var hours = seconds / SecondsPerHour;
        var remaining = seconds % SecondsPerHour;
        var minutes = remaining / SecondsPerMinute;
        var secs = remaining % SecondsPerMinute;

        return $"{hours:D2}:{minutes:D2}:{secs:D2}";
    }

    /// <summary>
    /// Formats a duration including milliseconds to HH:MM:SS.mmm format.
    /// </summary>
    /// <param name="seconds">The duration in seconds (can include fractional seconds).</param>
    /// <returns>A string in HH:MM:SS.mmm format.</returns>
    /// <exception cref="ArgumentException">Thrown when seconds is negative.</exception>
    public static string FormatWithMilliseconds(double seconds)
    {
        if (seconds < 0)
        {
            throw new ArgumentException("Duration cannot be negative.", nameof(seconds));
        }

        var totalSeconds = (long)seconds;
        var milliseconds = (int)((seconds - totalSeconds) * 1000);

        var baseFormat = Format(totalSeconds);
        return $"{baseFormat}.{milliseconds:D3}";
    }
}