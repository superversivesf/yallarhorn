namespace Yallarhorn.Logging;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Enrichers;

/// <summary>
/// Extension methods for configuring Serilog logging.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Configures Serilog with settings from configuration.
    /// </summary>
    public static WebApplicationBuilder AddYallarhornLogging(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration
            .GetSection(LoggingOptions.SectionName)
            .Get<LoggingOptions>() ?? new LoggingOptions();

        options.Validate();

        builder.AddYallarhornLogging(options);

        return builder;
    }

    /// <summary>
    /// Configures Serilog with the specified options.
    /// </summary>
    public static WebApplicationBuilder AddYallarhornLogging(this WebApplicationBuilder builder, LoggingOptions options)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(options.GetMinimumLogEventLevel())
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", "Yallarhorn");

        if (options.EnableConsole)
        {
            loggerConfiguration = loggerConfiguration.WriteTo.Console(
                outputTemplate: options.ConsoleOutputTemplate);
        }

        if (options.EnableFile)
        {
            loggerConfiguration = loggerConfiguration.WriteTo.File(
                path: options.FilePath,
                outputTemplate: options.FileOutputTemplate,
                rollingInterval: options.GetSerilogRollingInterval(),
                retainedFileCountLimit: options.RetainedFileCount);
        }

        Log.Logger = loggerConfiguration.CreateLogger();

        builder.Services.AddSerilog(Log.Logger, dispose: true);

        builder.Host.UseSerilog();

        return builder;
    }
}