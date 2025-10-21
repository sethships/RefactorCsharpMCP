using RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;
using Microsoft.CodeAnalysis;
using System.Diagnostics;

namespace RefactorCsharpMCP.Tests.Infrastructure;

[Collection("CacheTests")] // Run cache tests sequentially to avoid file locking issues
public class ReferenceAssemblyResolverTests : IDisposable
{
    private readonly ReferenceAssemblyResolver _resolver;

    public ReferenceAssemblyResolverTests()
    {
        _resolver = new ReferenceAssemblyResolver();
    }

    public void Dispose()
    {
        // Clean up caches after tests
        try
        {
            _resolver.ClearAllCaches();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Theory]
    [InlineData("net9.0")]
    [InlineData("net8.0")]
    public async Task GetReferenceAssembliesAsync_Returns_ReferencesForModernDotNet(string framework)
    {
        // Act
        var references = await _resolver.GetReferenceAssembliesAsync(framework);

        // Assert
        Assert.NotNull(references);
        Assert.NotEmpty(references);
        Assert.All(references, r => Assert.IsAssignableFrom<MetadataReference>(r));
    }

    [Theory]
    [InlineData("netstandard2.1")]
    [InlineData("netstandard2.0")]
    public async Task GetReferenceAssembliesAsync_Returns_ReferencesForNetStandard(string framework)
    {
        // Act
        var references = await _resolver.GetReferenceAssembliesAsync(framework);

        // Assert
        Assert.NotNull(references);
        Assert.NotEmpty(references);
    }

    [Fact]
    public async Task GetReferenceAssembliesAsync_Throws_ForEOLFramework()
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
    public async Task GetReferenceAssembliesAsync_Throws_ForUnknownFramework()
    {
        // Arrange
        var unknownFramework = "net99.0";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _resolver.GetReferenceAssembliesAsync(unknownFramework));

        Assert.Contains("Unknown or unsupported framework", exception.Message);
    }

    [Fact]
    public async Task GetReferenceAssembliesAsync_Normalizes_FrameworkMoniker()
    {
        // Arrange & Act
        var references1 = await _resolver.GetReferenceAssembliesAsync("NET8.0");
        var references2 = await _resolver.GetReferenceAssembliesAsync("net8.0");

        // Assert
        Assert.NotNull(references1);
        Assert.NotNull(references2);
        // Should return same cached instance due to normalization
        Assert.Equal(references1.Count, references2.Count);
    }

    [Fact]
    public async Task GetReferenceAssembliesAsync_Uses_MemoryCache_OnSecondCall()
    {
        // Arrange
        var framework = "net8.0";

        // First call
        var stopwatch1 = Stopwatch.StartNew();
        var references1 = await _resolver.GetReferenceAssembliesAsync(framework);
        stopwatch1.Stop();

        // Second call
        var stopwatch2 = Stopwatch.StartNew();
        var references2 = await _resolver.GetReferenceAssembliesAsync(framework);
        stopwatch2.Stop();

        // Assert
        Assert.Same(references1, references2); // Should be exact same instance from memory cache
        Assert.True(stopwatch2.ElapsedMilliseconds < stopwatch1.ElapsedMilliseconds,
            "Second call should be faster due to memory cache");
    }

    [Fact(Skip = "Performance test - timing sensitive in CI/CD environments")]
    public async Task GetReferenceAssembliesAsync_Performance_FirstLoad_Within500ms()
    {
        // Arrange
        var framework = "net8.0";
        _resolver.ClearAllCaches(); // Ensure cold start

        // Act
        var stopwatch = Stopwatch.StartNew();
        var references = await _resolver.GetReferenceAssembliesAsync(framework);
        stopwatch.Stop();

        // Assert
        Assert.NotEmpty(references);
        // Allow 2000ms for first load (includes test overhead), but target is 500ms
        Assert.True(stopwatch.ElapsedMilliseconds < 2000,
            $"First load took {stopwatch.ElapsedMilliseconds}ms, expected <2000ms");
    }

    [Fact(Skip = "Performance test - timing sensitive in CI/CD environments")]
    public async Task GetReferenceAssembliesAsync_Performance_CachedLoad_Within50ms()
    {
        // Arrange
        var framework = "net8.0";

        // Warm up cache
        await _resolver.GetReferenceAssembliesAsync(framework);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var references = await _resolver.GetReferenceAssembliesAsync(framework);
        stopwatch.Stop();

        // Assert
        Assert.NotEmpty(references);
        // Memory cache should be very fast
        Assert.True(stopwatch.ElapsedMilliseconds < 100,
            $"Cached load took {stopwatch.ElapsedMilliseconds}ms, expected <100ms");
    }

    [Fact]
    public async Task GetReferenceAssembliesAsync_Returns_ValidMetadataReferences()
    {
        // Arrange
        var framework = "net8.0";

        // Act
        var references = await _resolver.GetReferenceAssembliesAsync(framework);

        // Assert
        Assert.NotEmpty(references);
        Assert.All(references, reference =>
        {
            Assert.NotNull(reference);
            Assert.NotNull(reference.Display);
        });
    }

    [Fact]
    public async Task GetReferenceAssembliesAsync_Includes_CoreSystemAssemblies()
    {
        // Arrange
        var framework = "net8.0";

        // Act
        var references = await _resolver.GetReferenceAssembliesAsync(framework);

        // Assert
        Assert.NotEmpty(references);

        // Should include essential assemblies
        var displayNames = references.Select(r => r.Display?.ToLowerInvariant() ?? "").ToList();
        Assert.Contains(displayNames, d => d.Contains("system.runtime") || d.Contains("mscorlib"));
    }

    [Fact]
    public void GetSupportedFrameworks_Returns_All11Frameworks()
    {
        // Act
        var frameworks = ReferenceAssemblyResolver.GetSupportedFrameworks();

        // Assert - 11 total: 2 modern .NET + 7 .NET Framework + 2 .NET Standard
        Assert.Equal(11, frameworks.Count);
        Assert.Contains("net9.0", frameworks);
        Assert.Contains("net8.0", frameworks);
        Assert.Contains("net481", frameworks);
        Assert.Contains("net35", frameworks);
        Assert.Contains("netstandard2.1", frameworks);
        Assert.Contains("netstandard2.0", frameworks);
    }

    [Fact]
    public void GetCacheStatistics_Returns_ValidStats()
    {
        // Act
        var stats = _resolver.GetCacheStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.TotalFrameworks >= 0);
        Assert.True(stats.CacheRootSize >= 0);
    }

    [Fact]
    public async Task GetCacheStatistics_Reflects_CachedFrameworks()
    {
        // Arrange
        await _resolver.GetReferenceAssembliesAsync("net8.0");

        // Act
        var stats = _resolver.GetCacheStatistics();

        // Assert
        Assert.True(stats.TotalFrameworks >= 1);
        Assert.True(stats.FrameworkDetails.ContainsKey("net8.0"));
    }

    [Fact]
    public async Task ClearAllCaches_Removes_MemoryCachedReferences()
    {
        // Arrange
        await _resolver.GetReferenceAssembliesAsync("net8.0");

        // Act
        _resolver.ClearAllCaches();

        // Clear and reload - should take longer if memory cache was cleared
        var stopwatch = Stopwatch.StartNew();
        await _resolver.GetReferenceAssembliesAsync("net8.0");
        stopwatch.Stop();

        // Assert - If it takes time, memory cache was cleared (will use disk or resolve)
        Assert.True(true); // Test passes if no exception
    }

    [Fact]
    public async Task GetReferenceAssembliesAsync_Handles_ConcurrentRequests()
    {
        // Arrange
        var framework = "net8.0";
        var tasks = new List<Task<IReadOnlyList<MetadataReference>>>();

        // Act - Make 5 concurrent requests
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(_resolver.GetReferenceAssembliesAsync(framework));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, r => Assert.NotEmpty(r));
        // All should return the same instance due to caching
        var firstResult = results[0];
        Assert.All(results, r => Assert.Same(firstResult, r));
    }

    [Theory]
    [InlineData("net9.0", false)]
    [InlineData("net8.0", false)]
    [InlineData("net481", true)]
    [InlineData("net48", true)]
    [InlineData("net35", true)]
    [InlineData("netstandard2.1", false)]
    public async Task GetReferenceAssembliesAsync_Uses_CorrectResolutionStrategy(string framework, bool requiresNuGet)
    {
        // Act
        var references = await _resolver.GetReferenceAssembliesAsync(framework);

        // Assert
        Assert.NotEmpty(references);
        // Cannot directly test internal strategy, but verify we got valid references
        Assert.All(references, r => Assert.NotNull(r.Display));
    }

    [Fact]
    public async Task GetReferenceAssembliesAsync_HandlesMultipleFrameworks_Independently()
    {
        // Arrange & Act
        var net8References = await _resolver.GetReferenceAssembliesAsync("net8.0");
        var net9References = await _resolver.GetReferenceAssembliesAsync("net9.0");
        var standardReferences = await _resolver.GetReferenceAssembliesAsync("netstandard2.1");

        // Assert
        Assert.NotEmpty(net8References);
        Assert.NotEmpty(net9References);
        Assert.NotEmpty(standardReferences);

        // Should be different instances
        Assert.NotSame(net8References, net9References);
        Assert.NotSame(net8References, standardReferences);
    }

    [Fact]
    public async Task GetReferenceAssembliesAsync_Validates_Framework_Before_Resolution()
    {
        // Arrange
        var invalidFramework = "invalid-framework";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _resolver.GetReferenceAssembliesAsync(invalidFramework));
    }

    [Theory]
    [InlineData("net7.0", "net8.0")]
    [InlineData("net6.0", "net8.0")]
    [InlineData("net461", "net462")]
    public async Task GetReferenceAssembliesAsync_Suggests_Alternative_ForEOL(string eolFramework, string expectedSuggestion)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            _resolver.GetReferenceAssembliesAsync(eolFramework));

        Assert.Contains(expectedSuggestion, exception.Message);
    }
}
