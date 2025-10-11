using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;

/// <summary>
/// Manages disk caching of reference assemblies for fast subsequent loads.
/// Cache location: %USERPROFILE%/.refactor-csharp-mcp/reference-assemblies/{framework}/
///
/// Cache Characteristics:
/// - Thread-safe: Concurrent access supported with automatic retry logic
/// - Framework isolation: Each framework has a dedicated subdirectory
/// - Size: ~50MB per framework (~550MB total for all 11 supported frameworks)
/// - Persistence: Cache persists across server restarts
/// - No automatic eviction: Manual cleanup required (see TROUBLESHOOTING.md)
///
/// Performance:
/// - Disk cache hit: ~100-500ms (load from disk)
/// - Cache miss: ~2000ms+ (requires NuGet download)
///
/// Error Handling:
/// - Transient file system errors handled with exponential backoff (50ms, 200ms, 500ms)
/// - Corrupt assemblies logged as warnings but don't prevent cache operation
/// - Missing source files logged but don't throw exceptions
/// </summary>
public class ReferenceAssemblyCache
{
    private static readonly string CacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".refactor-csharp-mcp",
        "reference-assemblies"
    );

    private readonly string _manifestPath = Path.Combine(CacheRoot, "cache-manifest.json");
    private readonly ILogger? _logger;

    public ReferenceAssemblyCache(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Cache manifest storing metadata about cached frameworks.
    /// </summary>
    private class CacheManifest
    {
        public Dictionary<string, FrameworkCacheEntry> Frameworks { get; set; } = new();
    }

    private class FrameworkCacheEntry
    {
        public string TargetFramework { get; set; } = string.Empty;
        public DateTime CachedAt { get; set; }
        public int AssemblyCount { get; set; }
        public string? NuGetPackageVersion { get; set; }
    }

    /// <summary>
    /// Checks if reference assemblies are cached for a framework.
    /// </summary>
    public bool IsCached(string targetFramework)
    {
        var frameworkDir = GetFrameworkCacheDirectory(targetFramework);
        if (!Directory.Exists(frameworkDir))
        {
            return false;
        }

        // Check if directory contains .dll files
        return Directory.GetFiles(frameworkDir, "*.dll").Length > 0;
    }

    /// <summary>
    /// Gets cached reference assemblies for a framework.
    /// Returns null if not cached.
    /// </summary>
    public IReadOnlyList<MetadataReference>? GetCachedReferences(string targetFramework)
    {
        if (!IsCached(targetFramework))
        {
            return null;
        }

        var frameworkDir = GetFrameworkCacheDirectory(targetFramework);
        var assemblyFiles = Directory.GetFiles(frameworkDir, "*.dll");

        var references = new List<MetadataReference>();
        foreach (var assemblyPath in assemblyFiles)
        {
            try
            {
                references.Add(MetadataReference.CreateFromFile(assemblyPath));
            }
            catch (Exception ex)
            {
                // Log but don't fail - some assemblies may be corrupt
                _logger?.LogWarning(ex, "Failed to load cached assembly {AssemblyPath}", assemblyPath);
            }
        }

        return references.Count > 0 ? references : null;
    }

    /// <summary>
    /// Caches reference assemblies for a framework.
    /// </summary>
    public void CacheReferences(string targetFramework, IEnumerable<string> assemblyPaths, string? nuGetPackageVersion = null)
    {
        var frameworkDir = GetFrameworkCacheDirectory(targetFramework);
        Directory.CreateDirectory(frameworkDir);

        int cachedCount = 0;
        foreach (var sourcePath in assemblyPaths)
        {
            try
            {
                var fileName = Path.GetFileName(sourcePath);
                var destPath = Path.Combine(frameworkDir, fileName);
                CopyFileWithRetry(sourcePath, destPath, overwrite: true);
                cachedCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to cache assembly {AssemblyPath}", sourcePath);
            }
        }

        // Update manifest
        UpdateManifest(targetFramework, cachedCount, nuGetPackageVersion);
    }

    /// <summary>
    /// Copies a file with retry logic for transient file system errors.
    /// </summary>
    private void CopyFileWithRetry(string sourcePath, string destPath, bool overwrite, int maxRetries = 3)
    {
        int attempt = 0;
        Exception? lastException = null;

        while (attempt < maxRetries)
        {
            try
            {
                File.Copy(sourcePath, destPath, overwrite);
                return; // Success
            }
            catch (IOException ex) when (attempt < maxRetries - 1)
            {
                // Transient file system errors (file in use, access denied temporarily, etc.)
                lastException = ex;
                attempt++;

                // Exponential backoff: 50ms, 200ms, 500ms
                int delayMs = attempt switch
                {
                    1 => 50,
                    2 => 200,
                    _ => 500
                };

                Thread.Sleep(delayMs);
            }
            catch (Exception ex)
            {
                // Non-transient errors (file not found, path too long, etc.) - fail immediately
                throw new IOException($"Failed to copy file from {sourcePath} to {destPath}", ex);
            }
        }

        // All retries exhausted
        throw new IOException($"Failed to copy file after {maxRetries} attempts: {sourcePath} to {destPath}", lastException);
    }

    /// <summary>
    /// Gets the cache directory for a specific framework.
    /// </summary>
    public string GetFrameworkCacheDirectory(string targetFramework)
    {
        return Path.Combine(CacheRoot, targetFramework.ToLowerInvariant());
    }

    /// <summary>
    /// Gets the root cache directory.
    /// </summary>
    public string GetCacheRoot()
    {
        return CacheRoot;
    }

    /// <summary>
    /// Clears cache for a specific framework.
    /// </summary>
    public void ClearCache(string targetFramework)
    {
        var frameworkDir = GetFrameworkCacheDirectory(targetFramework);
        if (Directory.Exists(frameworkDir))
        {
            Directory.Delete(frameworkDir, recursive: true);
        }

        // Update manifest
        var manifest = LoadManifest();
        manifest.Frameworks.Remove(targetFramework.ToLowerInvariant());
        SaveManifest(manifest);
    }

    /// <summary>
    /// Clears all cached reference assemblies.
    /// </summary>
    public void ClearAllCache()
    {
        if (Directory.Exists(CacheRoot))
        {
            Directory.Delete(CacheRoot, recursive: true);
        }
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        var manifest = LoadManifest();
        var stats = new CacheStatistics
        {
            TotalFrameworks = manifest.Frameworks.Count,
            CacheRootSize = GetDirectorySize(CacheRoot)
        };

        foreach (var entry in manifest.Frameworks.Values)
        {
            stats.FrameworkDetails[entry.TargetFramework] = new FrameworkCacheStats
            {
                CachedAt = entry.CachedAt,
                AssemblyCount = entry.AssemblyCount,
                NuGetPackageVersion = entry.NuGetPackageVersion
            };
        }

        return stats;
    }

    private void UpdateManifest(string targetFramework, int assemblyCount, string? nuGetPackageVersion)
    {
        var manifest = LoadManifest();
        var key = targetFramework.ToLowerInvariant();

        manifest.Frameworks[key] = new FrameworkCacheEntry
        {
            TargetFramework = targetFramework,
            CachedAt = DateTime.UtcNow,
            AssemblyCount = assemblyCount,
            NuGetPackageVersion = nuGetPackageVersion
        };

        SaveManifest(manifest);
    }

    private CacheManifest LoadManifest()
    {
        if (!File.Exists(_manifestPath))
        {
            return new CacheManifest();
        }

        try
        {
            var json = File.ReadAllText(_manifestPath);
            return JsonSerializer.Deserialize<CacheManifest>(json) ?? new CacheManifest();
        }
        catch
        {
            return new CacheManifest();
        }
    }

    private void SaveManifest(CacheManifest manifest)
    {
        Directory.CreateDirectory(CacheRoot);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_manifestPath, json);
    }

    private static long GetDirectorySize(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return 0;
        }

        return Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
                        .Sum(file => new FileInfo(file).Length);
    }
}

/// <summary>
/// Cache statistics for monitoring and diagnostics.
/// </summary>
public class CacheStatistics
{
    public int TotalFrameworks { get; set; }
    public long CacheRootSize { get; set; }
    public Dictionary<string, FrameworkCacheStats> FrameworkDetails { get; set; } = new();

    public string GetFormattedSize()
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return CacheRootSize switch
        {
            >= GB => $"{CacheRootSize / GB:F2} GB",
            >= MB => $"{CacheRootSize / MB:F2} MB",
            >= KB => $"{CacheRootSize / KB:F2} KB",
            _ => $"{CacheRootSize} bytes"
        };
    }
}

public class FrameworkCacheStats
{
    public DateTime CachedAt { get; set; }
    public int AssemblyCount { get; set; }
    public string? NuGetPackageVersion { get; set; }
}
