using Microsoft.EntityFrameworkCore;
using Yallarhorn.Data;

namespace Yallarhorn.Data.Initialization;

/// <summary>
/// Handles database initialization and schema creation.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Initializes the database, creating it if necessary.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task InitializeAsync(YallarhornDbContext context, CancellationToken cancellationToken = default)
    {
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }

    /// <summary>
    /// Checks if the database exists.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the database exists.</returns>
    public static async Task<bool> ExistsAsync(YallarhornDbContext context, CancellationToken cancellationToken = default)
    {
        return await context.Database.CanConnectAsync(cancellationToken);
    }

    /// <summary>
    /// Clears all data from the database (for testing).
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task ClearAsync(YallarhornDbContext context, CancellationToken cancellationToken = default)
    {
        context.DownloadQueue.RemoveRange(context.DownloadQueue);
        context.Episodes.RemoveRange(context.Episodes);
        context.Channels.RemoveRange(context.Channels);
        context.SchemaVersions.RemoveRange(context.SchemaVersions);
        await context.SaveChangesAsync(cancellationToken);
    }
}