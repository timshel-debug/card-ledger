using Microsoft.Data.Sqlite;

namespace CardService.Infrastructure;

/// <summary>
/// Helper class to ensure SQLite database file directories exist before EF Core operations.
/// </summary>
/// <remarks>
/// SQLite file-based databases require the parent directory to exist. This bootstrapper
/// parses the connection string and creates the directory if needed, avoiding runtime
/// failures when starting from a fresh checkout.
/// </remarks>
public static class SqliteBootstrapper
{
    /// <summary>
    /// Ensures that the directory for a file-based SQLite database exists.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string to parse.</param>
    /// <param name="basePath">Optional base path for resolving relative DataSource paths. If null, uses current directory.</param>
    /// <remarks>
    /// <para>
    /// If the DataSource is a file path (not :memory:), the parent directory is created
    /// if it doesn't exist. For in-memory databases, this method does nothing.
    /// </para>
    /// <para>
    /// When basePath is provided and DataSource is relative, the path is resolved against
    /// basePath (typically the application's ContentRootPath) for deterministic placement.
    /// </para>
    /// </remarks>
    public static void EnsureDirectoryExists(string connectionString, string? basePath = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource;

        // Skip in-memory databases
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
            return;

        // Resolve path: if basePath provided and dataSource is relative, combine them
        var dataSourceFullPath = dataSource;
        if (!string.IsNullOrWhiteSpace(basePath) && !Path.IsPathRooted(dataSource))
        {
            dataSourceFullPath = Path.GetFullPath(Path.Combine(basePath, dataSource));
        }
        else
        {
            dataSourceFullPath = Path.GetFullPath(dataSource);
        }

        // Ensure the directory exists for file-based databases
        var directory = Path.GetDirectoryName(dataSourceFullPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
