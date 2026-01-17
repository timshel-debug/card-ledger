using CardService.Infrastructure;
using AwesomeAssertions;

namespace CardService.Api.Tests;

public class SqliteBootstrapperTests
{
    [Fact]
    public void EnsureDirectoryExists_WithRelativePath_CreatesDirectoryUnderBasePath()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var connectionString = "Data Source=App_Data/app.db";

        try
        {
            // Act
            SqliteBootstrapper.EnsureDirectoryExists(connectionString, tempRoot);

            // Assert
            var expectedDir = Path.Combine(tempRoot, "App_Data");
            Directory.Exists(expectedDir).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsureDirectoryExists_WithAbsolutePath_CreatesDirectoryAtAbsolutePath()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(tempRoot, "data", "test.db");
        var connectionString = $"Data Source={dbPath}";

        try
        {
            // Act
            SqliteBootstrapper.EnsureDirectoryExists(connectionString, basePath: null);

            // Assert
            var expectedDir = Path.Combine(tempRoot, "data");
            Directory.Exists(expectedDir).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsureDirectoryExists_WithInMemoryDatabase_DoesNotCreateDirectory()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var connectionString = "Data Source=:memory:";

        try
        {
            // Act
            SqliteBootstrapper.EnsureDirectoryExists(connectionString, tempRoot);

            // Assert - tempRoot should not be created for in-memory DB
            Directory.Exists(tempRoot).Should().BeFalse();
        }
        finally
        {
            // Cleanup (should be nothing, but just in case)
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsureDirectoryExists_WithSharedMemoryDatabase_DoesNotCreateDirectory()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var connectionString = "Data Source=:memory:;Cache=Shared";

        try
        {
            // Act
            SqliteBootstrapper.EnsureDirectoryExists(connectionString, tempRoot);

            // Assert - tempRoot should not be created for in-memory DB
            Directory.Exists(tempRoot).Should().BeFalse();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsureDirectoryExists_WithNullConnectionString_DoesNotThrow()
    {
        // Act & Assert - should not throw
        var exception = Record.Exception(() => 
            SqliteBootstrapper.EnsureDirectoryExists(null!, basePath: "/some/path"));
        
        exception.Should().BeNull();
    }

    [Fact]
    public void EnsureDirectoryExists_WithEmptyConnectionString_DoesNotThrow()
    {
        // Act & Assert - should not throw
        var exception = Record.Exception(() => 
            SqliteBootstrapper.EnsureDirectoryExists(string.Empty, basePath: "/some/path"));
        
        exception.Should().BeNull();
    }
}
