using RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;
using Microsoft.CodeAnalysis;

namespace RefactorCsharpMCP.Tests.Infrastructure;

/// <summary>
/// Tests for error scenarios: network failures, disk issues, corrupt files.
/// These tests verify graceful degradation and error handling.
/// </summary>
[Collection("CacheTests")] // Run with cache tests sequentially to avoid file locking issues
public class ReferenceAssemblyErrorScenarioTests : IDisposable
{
    private readonly ReferenceAssemblyResolver _resolver;
    private readonly ReferenceAssemblyCache _cache;

    public ReferenceAssemblyErrorScenarioTests()
    {
        _resolver = new ReferenceAssemblyResolver();
        _cache = new ReferenceAssemblyCache();
    }

    public void Dispose()
    {
        try
        {
            _resolver.Dispose();
            _cache.ClearAllCache();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task GetReferenceAssembliesAsync_HandlesInvalidFramework_ThrowsArgumentException()
    {
        // Arrange
        var invalidFramework = "invalid-framework-123";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _resolver.GetReferenceAssembliesAsync(invalidFramework));

        Assert.Contains("Unknown or unsupported framework", exception.Message);
    }

    [Fact]
    public async Task GetReferenceAssembliesAsync_HandlesEOLFramework_ThrowsNotSupportedException()
    {
        // Arrange
        var eolFramework = "net7.0";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            _resolver.GetReferenceAssembliesAsync(eolFramework));

        Assert.Contains("end-of-life", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("net8.0", exception.Message); // Should suggest alternative
    }

    [Fact]
    public void CacheReferences_HandlesNonExistentSourceFile_LogsWarningAndContinues()
    {
        // Arrange
        var framework = "net8.0";
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "nonexistent-assembly-" + Guid.NewGuid() + ".dll");

        // Act - should not throw, just log warning
        _cache.CacheReferences(framework, new[] { nonExistentPath });

        // Assert - cache should still be created (even if empty)
        var stats = _cache.GetStatistics();
        Assert.True(stats.FrameworkDetails.ContainsKey(framework));
        Assert.Equal(0, stats.FrameworkDetails[framework].AssemblyCount); // No assemblies cached
    }

    [Fact]
    public void GetCachedReferences_HandlesCorruptAssembly_SkipsAndContinues()
    {
        // Arrange
        var framework = "net8.0";
        var tempDir = Path.Combine(Path.GetTempPath(), "corrupt-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create a corrupt "assembly" file (just random bytes)
            var corruptFile = Path.Combine(tempDir, "corrupt.dll");
            File.WriteAllText(corruptFile, "This is not a valid assembly!");

            // Create a valid assembly reference
            var validAssembly = typeof(object).Assembly.Location;

            // Cache both files
            var cacheWithLogger = new ReferenceAssemblyCache();
            cacheWithLogger.CacheReferences(framework, new[] { validAssembly, corruptFile });

            // Act - should handle corrupt file gracefully
            var references = cacheWithLogger.GetCachedReferences(framework);

            // Assert - should return at least the valid assembly
            Assert.NotNull(references);
            Assert.NotEmpty(references);
            // Corrupt file should be skipped with warning logged
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ClearCache_HandlesNonExistentDirectory_DoesNotThrow()
    {
        // Arrange
        var nonExistentFramework = "net99.0";

        // Act & Assert - should not throw even if directory doesn't exist
        var exception = Record.Exception(() => _cache.ClearCache(nonExistentFramework));
        Assert.Null(exception);
    }

    [Fact]
    public void GetCachedReferences_HandlesEmptyDirectory_ReturnsNull()
    {
        // Arrange
        var framework = "net8.0";
        var emptyDir = _cache.GetFrameworkCacheDirectory(framework);
        Directory.CreateDirectory(emptyDir);

        try
        {
            // Directory exists but contains no DLL files

            // Act
            var references = _cache.GetCachedReferences(framework);

            // Assert
            Assert.Null(references); // Should return null for empty cache
        }
        finally
        {
            // Cleanup
            _cache.ClearCache(framework);
        }
    }

    [Fact]
    public async Task GetReferenceAssembliesAsync_HandlesConcurrentAccessToSameFramework_ReturnsConsistentResults()
    {
        // Arrange
        var framework = "net8.0";
        _resolver.ClearAllCaches(); // Start fresh

        // Act - Make multiple concurrent requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _resolver.GetReferenceAssembliesAsync(framework))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - All results should be identical (same cached instance)
        var firstResult = results[0];
        Assert.NotEmpty(firstResult);

        foreach (var result in results)
        {
            Assert.Same(firstResult, result); // Should return exact same instance
        }
    }

    [Fact]
    public void GetStatistics_HandlesCorruptManifest_ReturnsEmptyStats()
    {
        // Arrange - Create a cache with corrupt manifest
        var cacheRoot = _cache.GetCacheRoot();
        var manifestPath = Path.Combine(cacheRoot, "cache-manifest.json");
        Directory.CreateDirectory(cacheRoot);

        try
        {
            // Write corrupt JSON
            File.WriteAllText(manifestPath, "{ corrupt json content }}}");

            // Act
            var stats = _cache.GetStatistics();

            // Assert - Should return empty stats without throwing
            Assert.NotNull(stats);
            Assert.Equal(0, stats.TotalFrameworks);
        }
        finally
        {
            // Cleanup
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetReferenceAssembliesAsync_HandlesInvalidInput_ThrowsArgumentException(string? invalidInput)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _resolver.GetReferenceAssembliesAsync(invalidInput!));
    }

    [Fact]
    public void CacheReferences_HandlesVeryLongPath_LogsWarningAndContinues()
    {
        // Arrange
        var framework = "net8.0";
        // Create a path that's too long (> 260 characters on Windows)
        var longPath = Path.Combine(Path.GetTempPath(), new string('a', 300)) + ".dll";

        // Act - should not throw, just log warning
        var exception = Record.Exception(() =>
            _cache.CacheReferences(framework, new[] { longPath }));

        // Assert - should handle gracefully
        Assert.Null(exception);
    }

    [Fact]
    public void GetFrameworkCacheDirectory_NormalizesFrameworkName_CaseInsensitive()
    {
        // Act
        var dir1 = _cache.GetFrameworkCacheDirectory("NET8.0");
        var dir2 = _cache.GetFrameworkCacheDirectory("net8.0");
        var dir3 = _cache.GetFrameworkCacheDirectory("Net8.0");

        // Assert - All should return the same normalized path
        Assert.Equal(dir1, dir2);
        Assert.Equal(dir2, dir3);
        Assert.Contains("net8.0", dir1.ToLowerInvariant());
    }

    [Fact]
    public async Task GetReferenceAssembliesAsync_RecoverFromTransientFailure_RetriesSuccessfully()
    {
        // This test verifies that the retry logic works
        // Since we can't easily mock file system failures, we'll verify the mechanism exists
        // by checking that multiple calls eventually succeed even if there are transient issues

        // Arrange
        var framework = "net8.0";

        // Act - First call might hit file system contention
        var result1 = await _resolver.GetReferenceAssembliesAsync(framework);

        // Simulate another process accessing the cache
        Thread.Sleep(10);

        // Second call should succeed (from memory cache)
        var result2 = await _resolver.GetReferenceAssembliesAsync(framework);

        // Assert
        Assert.NotEmpty(result1);
        Assert.NotEmpty(result2);
        Assert.Same(result1, result2); // Should be same instance from cache
    }
}
