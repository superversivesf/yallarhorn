namespace Yallarhorn.Tests.Integration;

using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Yallarhorn.Data;

/// <summary>
/// Integration tests verifying database initialization at startup.
/// Tests ensure the database is created correctly, migrations are applied,
/// and schema is properly configured using EF Core's EnsureCreated pattern.
/// </summary>
public class DatabaseInitializationTests : IDisposable
{
    private readonly List<SqliteConnection> _connections = [];
    private readonly List<string> _tempDbFiles = [];

    public void Dispose()
    {
        foreach (var connection in _connections)
        {
            connection.Close();
            connection.Dispose();
        }

        foreach (var file in _tempDbFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        GC.SuppressFinalize(this);
    }

    #region In-Memory SQLite Tests

    [Fact]
    public void EnsureCreated_WithInMemorySqlite_ShouldCreateSchema()
    {
        // Arrange - Use connection that stays open for in-memory SQLite
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        // Act
        using var context = new YallarhornDbContext(options);
        var created = context.Database.EnsureCreated();

        // Assert
        created.Should().BeTrue("database should be created fresh");
        context.Database.IsSqlite().Should().BeTrue();
    }

    [Fact]
    public void EnsureCreated_WhenCalledTwice_ShouldReturnFalseSecondTime()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        // Act
        using var context1 = new YallarhornDbContext(options);
        var firstCreate = context1.Database.EnsureCreated();

        using var context2 = new YallarhornDbContext(options);
        var secondCreate = context2.Database.EnsureCreated();

        // Assert
        firstCreate.Should().BeTrue("database created for first time");
        secondCreate.Should().BeFalse("database already exists");
    }

    [Fact]
    public void DbContext_WithInMemorySqlite_ShouldHaveAllDbSets()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        // Act
        using var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();

        // Assert
        context.Channels.Should().NotBeNull("Channels DbSet is required");
        context.Episodes.Should().NotBeNull("Episodes DbSet is required");
        context.DownloadQueue.Should().NotBeNull("DownloadQueue DbSet is required");
        context.SchemaVersions.Should().NotBeNull("SchemaVersions DbSet is required");
    }

    #endregion

    #region File-Based SQLite Tests

    [Fact]
    public void EnsureCreated_WithFileBasedSqlite_ShouldCreateDatabaseFile()
    {
        // Arrange
        var dbPath = Path.Combine(Path.GetTempPath(), $"yallarhorn_init_test_{Guid.NewGuid()}.db");
        _tempDbFiles.Add(dbPath);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        // Act
        using var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();

        // Assert
        File.Exists(dbPath).Should().BeTrue("database file should be created");
    }

    [Fact]
    public void DatabaseFile_WhenCreated_ShouldExistAndHaveContent()
    {
        // Arrange
        var dbPath = Path.Combine(Path.GetTempPath(), $"yallarhorn_write_test_{Guid.NewGuid()}.db");
        _tempDbFiles.Add(dbPath);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        // Act - Create and dispose context to release file handle
        using (var context = new YallarhornDbContext(options))
        {
            context.Database.EnsureCreated();
        }

        // Assert - File should exist with content (SQLite database file)
        File.Exists(dbPath).Should().BeTrue("database file should be created");
        var fileInfo = new FileInfo(dbPath);
        fileInfo.Length.Should().BeGreaterThan(0, "database file should have content after initialization");
    }

    [Fact]
    public void ConnectionString_WithRelativePath_ShouldWork()
    {
        // Arrange
        var fileName = $"yallarhorn_relative_{Guid.NewGuid()}.db";
        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite($"Data Source={fileName}")
            .Options;

        // Act & Assert - Should not throw
        using var context = new YallarhornDbContext(options);
        var act = () => context.Database.EnsureCreated();
        act.Should().NotThrow();

        // Cleanup
        if (File.Exists(fileName))
        {
            File.Delete(fileName);
        }
    }

    #endregion

    #region Schema Verification Tests

    [Fact]
    public async Task Schema_ShouldHaveChannelsTable()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();

        // Act
        var tables = await GetTableNames(connection);

        // Assert
        tables.Should().Contain("channels", "channels table is required");
    }

    [Fact]
    public async Task Schema_ShouldHaveEpisodesTable()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();

        // Act
        var tables = await GetTableNames(connection);

        // Assert
        tables.Should().Contain("episodes", "episodes table is required");
    }

    [Fact]
    public async Task Schema_ShouldHaveDownloadQueueTable()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();

        // Act
        var tables = await GetTableNames(connection);

        // Assert
        tables.Should().Contain("download_queue", "download_queue table is required");
    }

    [Fact]
    public async Task Schema_ShouldHaveSchemaVersionsTable()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();

        // Act
        var tables = await GetTableNames(connection);

        // Assert
        tables.Should().Contain("schema_version", "schema_version table is required for migration tracking");
    }

    [Fact]
    public async Task ChannelsTable_ShouldHaveRequiredColumns()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();

        // Act
        var columns = await GetColumnNames(connection, "channels");

        // Assert
        columns.Should().Contain("id", "id column is primary key");
        columns.Should().Contain("url", "url column is required");
        columns.Should().Contain("title", "title column is required");
        columns.Should().Contain("created_at", "created_at timestamp is required");
        columns.Should().Contain("updated_at", "updated_at timestamp is required");
    }

    [Fact]
    public async Task EpisodesTable_ShouldHaveRequiredColumns()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();

        // Act
        var columns = await GetColumnNames(connection, "episodes");

        // Assert
        columns.Should().Contain("id", "id column is primary key");
        columns.Should().Contain("video_id", "video_id is for YouTube deduplication");
        columns.Should().Contain("channel_id", "channel_id is foreign key");
        columns.Should().Contain("title", "title column is required");
        columns.Should().Contain("status", "status tracks download state");
    }

    [Fact]
    public async Task DownloadQueueTable_ShouldHaveRequiredColumns()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();

        // Act
        var columns = await GetColumnNames(connection, "download_queue");

        // Assert
        columns.Should().Contain("id", "id column is primary key");
        columns.Should().Contain("episode_id", "episode_id links to episode");
        columns.Should().Contain("priority", "priority for queue ordering");
        columns.Should().Contain("status", "status tracks queue state");
    }

    #endregion

    #region Index Verification Tests

    [Fact]
    public async Task Schema_ShouldHaveUniqueIndexOnChannelUrl()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();

        // Act
        var indexes = await GetIndexes(connection, "channels");

        // Assert
        indexes.Should().Contain(i => i.Contains("url"), "channel URL should be indexed for uniqueness");
    }

    [Fact]
    public async Task Schema_ShouldHaveUniqueIndexOnEpisodeVideoId()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();

        // Act
        var indexes = await GetIndexes(connection, "episodes");

        // Assert
        indexes.Should().Contain(i => i.Contains("video_id"), "episode video_id should be indexed for uniqueness");
    }

    [Fact]
    public async Task Schema_ShouldHaveIndexOnEpisodeStatus()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();

        // Act
        var indexes = await GetIndexes(connection, "episodes");

        // Assert
        indexes.Should().Contain(i => i.Contains("status"), "episode status should be indexed for filtering");
    }

    [Fact]
    public async Task Schema_ShouldHaveIndexOnDownloadQueueStatusAndPriority()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();

        // Act
        var indexes = await GetIndexes(connection, "download_queue");

        // Assert
        indexes.Should().Contain(i => i.Contains("status"), "download queue status should be indexed");
    }

    [Fact]
    public async Task Schema_ShouldHaveIndexOnEpisodeChannelId()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();

        // Act
        var indexes = await GetIndexes(connection, "episodes");

        // Assert
        indexes.Should().Contain(i => i.Contains("channel_id"), "episode channel_id FK should be indexed");
    }

    #endregion

    #region DbContext Disposal Tests

    [Fact]
    public void DbContext_WhenDisposed_ShouldReleaseResources()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        // Act
        var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();
        context.Dispose();

        // Assert - should not throw on second dispose
        var act = () => context.Dispose();
        act.Should().NotThrow("Dispose should be idempotent");
    }

    [Fact]
    public async Task DbContext_WhenDisposed_ShouldNotAllowOperations()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();
        context.Dispose();

        // Act & Assert
        var act = async () => await context.Channels.CountAsync();
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Foreign Key Relationship Tests

    [Fact]
    public async Task ForeignKey_EpisodeToChannel_ShouldBeConfigured()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();

        // Act - Check foreign key configuration via model metadata
        var episodeEntity = context.Model.FindEntityType(typeof(Yallarhorn.Data.Entities.Episode));
        var channelNavigation = episodeEntity?.FindNavigation(nameof(Yallarhorn.Data.Entities.Episode.Channel));

        // Assert
        channelNavigation.Should().NotBeNull("Episode should have navigation to Channel");
        channelNavigation!.ForeignKey.PrincipalEntityType.ClrType.Should().Be(typeof(Yallarhorn.Data.Entities.Channel));
    }

    [Fact]
    public async Task ForeignKey_DownloadQueueToEpisode_ShouldBeConfigured()
    {
        // Arrange
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<YallarhornDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new YallarhornDbContext(options);
        context.Database.EnsureCreated();

        // Act
        var queueEntity = context.Model.FindEntityType(typeof(Yallarhorn.Data.Entities.DownloadQueue));
        var episodeNavigation = queueEntity?.FindNavigation(nameof(Yallarhorn.Data.Entities.DownloadQueue.Episode));

        // Assert
        episodeNavigation.Should().NotBeNull("DownloadQueue should have navigation to Episode");
        episodeNavigation!.ForeignKey.PrincipalEntityType.ClrType.Should().Be(typeof(Yallarhorn.Data.Entities.Episode));
    }

    #endregion

    #region Helper Methods

    private static async Task<List<string>> GetTableNames(SqliteConnection connection)
    {
        var tables = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static async Task<List<string>> GetColumnNames(SqliteConnection connection, string tableName)
    {
        var columns = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1)); // name is column index 1
        }

        return columns;
    }

    private static async Task<List<string>> GetIndexes(SqliteConnection connection, string tableName)
    {
        var indexes = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='{tableName}';";

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        return indexes;
    }

    #endregion
}
