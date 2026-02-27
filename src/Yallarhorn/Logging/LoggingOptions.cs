namespace Yallarhorn.Logging;

using Serilog.Events;

/// <summary>
/// Configuration options for logging.
/// </summary>
public class LoggingOptions
{
    /// <summary>
    /// The section name in appsettings.json.
    /// </summary>
    public const string SectionName = "LoggingOptions";

    /// <summary>
    /// Gets or sets the minimum log level.
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Gets or sets whether to enable console logging.
    /// </summary>
    public bool EnableConsole { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable file logging.
    /// </summary>
    public bool EnableFile { get; set; } = true;

    /// <summary>
    /// Gets or sets the log file path template.
    /// </summary>
    public string FilePath { get; set; } = "logs/yallarhorn-.log";

    /// <summary>
    /// Gets or sets the file rolling interval.
    /// </summary>
    public string RollingInterval { get; set; } = "Day";

    /// <summary>
    /// Gets or sets the retained file count.
    /// </summary>
    public int? RetainedFileCount { get; set; } = 31;

    /// <summary>
    /// Gets or sets the output template for console.
    /// </summary>
    public string ConsoleOutputTemplate { get; set; } = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Gets or sets the output template for file.
    /// </summary>
    public string FileOutputTemplate { get; set; } = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Parses the minimum level string to LogEventLevel.
    /// </summary>
    public LogEventLevel GetMinimumLogEventLevel()
    {
        return MinimumLevel?.ToLowerInvariant() switch
        {
            "verbose" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" or "info" => LogEventLevel.Information,
            "warning" or "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }

    /// <summary>
    /// Gets the rolling interval for Serilog.
    /// </summary>
    public Serilog.RollingInterval GetSerilogRollingInterval()
    {
        return RollingInterval?.ToLowerInvariant() switch
        {
            "minute" => Serilog.RollingInterval.Minute,
            "hour" => Serilog.RollingInterval.Hour,
            "day" => Serilog.RollingInterval.Day,
            "month" => Serilog.RollingInterval.Month,
            "year" => Serilog.RollingInterval.Year,
            "infinite" => Serilog.RollingInterval.Infinite,
            _ => Serilog.RollingInterval.Day
        };
    }

    /// <summary>
    /// Validates the options and throws if invalid.
    /// </summary>
    public void Validate()
    {
        var validLevels = new[] { "verbose", "debug", "information", "info", "warning", "warn", "error", "fatal" };
        if (!validLevels.Contains(MinimumLevel?.ToLowerInvariant()))
            throw new ArgumentException($"Invalid minimum log level: {MinimumLevel}");

        if (RetainedFileCount.HasValue && RetainedFileCount.Value < 1)
            throw new ArgumentOutOfRangeException(nameof(RetainedFileCount), "RetainedFileCount must be at least 1");
    }
}