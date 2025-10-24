using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;

/// <summary>
/// Main orchestrator for resolving reference assemblies across all 11 supported .NET frameworks.
/// Handles framework validation, caching, NuGet downloads, and returns MetadataReference[] for Roslyn compilation.
/// </summary>
public class ReferenceAssemblyResolver : IDisposable
{
    private readonly ReferenceAssemblyCache _cache;
    private readonly NuGetPackageDownloader _downloader;
    private readonly ConcurrentDictionary<string, IReadOnlyList<MetadataReference>> _memoryCache;
    private readonly ILogger? _logger;

    public ReferenceAssemblyResolver(ILogger? logger = null)
    {
        _logger = logger;
        _cache = new ReferenceAssemblyCache(logger);
        _downloader = new NuGetPackageDownloader(logger: logger);
        _memoryCache = new ConcurrentDictionary<string, IReadOnlyList<MetadataReference>>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets reference assemblies for a target framework.
    /// Flow: Validate → Check memory cache → Check disk cache → Download → Cache → Return
    /// </summary>
    /// <param name="targetFramework">Target framework moniker (e.g., "net8.0", "net48", "net35")</param>
    /// <returns>MetadataReference array for Roslyn compilation</returns>
    public async Task<IReadOnlyList<MetadataReference>> GetReferenceAssembliesAsync(string targetFramework)
    {
        var stopwatch = Stopwatch.StartNew();

        // Normalize framework moniker
        targetFramework = FrameworkMoniker.Normalize(targetFramework);

        // Validate framework
        ValidateFramework(targetFramework);

        // Check memory cache first (fastest)
        if (_memoryCache.TryGetValue(targetFramework, out var cachedReferences))
        {
            _logger?.LogInformation("[ReferenceAssemblyResolver] Memory cache hit for {TargetFramework} ({ElapsedMs}ms)",
                targetFramework, stopwatch.ElapsedMilliseconds);
            return cachedReferences;
        }

        // Check disk cache
        var diskCachedReferences = _cache.GetCachedReferences(targetFramework);
        if (diskCachedReferences != null)
        {
            _memoryCache[targetFramework] = diskCachedReferences;
            _logger?.LogInformation("[ReferenceAssemblyResolver] Disk cache hit for {TargetFramework} ({ElapsedMs}ms)",
                targetFramework, stopwatch.ElapsedMilliseconds);
            return diskCachedReferences;
        }

        // Cache miss - need to download/resolve
        _logger?.LogInformation("[ReferenceAssemblyResolver] Cache miss for {TargetFramework}, resolving...", targetFramework);

        IReadOnlyList<MetadataReference> references;

        if (FrameworkMoniker.RequiresNuGetPackage(targetFramework))
        {
            // .NET Framework - download from NuGet
            references = await ResolveFromNuGetAsync(targetFramework);
        }
        else
        {
            // Modern .NET or .NET Standard - use runtime assemblies
            references = ResolveFromRuntime(targetFramework);
        }

        // Cache in memory
        _memoryCache[targetFramework] = references;

        stopwatch.Stop();
        _logger?.LogInformation("[ReferenceAssemblyResolver] Resolved {ReferenceCount} references for {TargetFramework} ({ElapsedMs}ms)",
            references.Count, targetFramework, stopwatch.ElapsedMilliseconds);

        return references;
    }

    /// <summary>
    /// Validates that a framework moniker is supported.
    /// </summary>
    private void ValidateFramework(string targetFramework)
    {
        if (FrameworkMoniker.IsEndOfLife(targetFramework))
        {
            var alternative = FrameworkMoniker.SuggestAlternative(targetFramework);
            var friendlyName = FrameworkMoniker.GetFriendlyName(targetFramework);

            throw new NotSupportedException(
                $"Framework '{friendlyName}' is end-of-life and not supported. " +
                (alternative != null
                    ? $"Please use '{FrameworkMoniker.GetFriendlyName(alternative)}' ({alternative}) instead."
                    : "Please use a currently supported framework.")
            );
        }

        if (!FrameworkMoniker.IsSupported(targetFramework))
        {
            throw new ArgumentException(
                $"Unknown or unsupported framework: '{targetFramework}'. " +
                $"Supported frameworks: {string.Join(", ", FrameworkMoniker.SupportedFrameworks)}",
                nameof(targetFramework)
            );
        }
    }

    /// <summary>
    /// Resolves reference assemblies from NuGet packages (.NET Framework).
    /// </summary>
    private async Task<IReadOnlyList<MetadataReference>> ResolveFromNuGetAsync(string targetFramework)
    {
        var packageName = FrameworkMoniker.GetNuGetPackageName(targetFramework);
        if (packageName == null)
        {
            throw new InvalidOperationException($"No NuGet package defined for framework: {targetFramework}");
        }

        // Download and extract package
        var assemblyPaths = await _downloader.DownloadAndExtractAsync(packageName, targetFramework);

        if (assemblyPaths.Count == 0)
        {
            throw new InvalidOperationException($"No reference assemblies found in package: {packageName}");
        }

        // Cache to disk for future use
        _cache.CacheReferences(targetFramework, assemblyPaths, packageName);

        // Create MetadataReferences
        var references = new List<MetadataReference>();
        foreach (var path in assemblyPaths)
        {
            try
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to create reference from {AssemblyPath}", path);
            }
        }

        return references;
    }

    /// <summary>
    /// Resolves reference assemblies from current runtime (.NET 8/9, .NET Standard).
    /// </summary>
    private IReadOnlyList<MetadataReference> ResolveFromRuntime(string targetFramework)
    {
        var references = new List<MetadataReference>();

        // Get core runtime assemblies
        var runtimeAssemblies = GetRuntimeAssemblies(targetFramework);

        var assemblyPaths = new List<string>();

        foreach (var assemblyName in runtimeAssemblies)
        {
            try
            {
                // Try to load assembly and get its location
                var assembly = Assembly.Load(new AssemblyName(assemblyName));
                if (!string.IsNullOrEmpty(assembly.Location))
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                    assemblyPaths.Add(assembly.Location);
                }
            }
            catch
            {
                // Assembly not available in current runtime - skip
            }
        }

        // Add basic .NET runtime assemblies from current process
        var coreAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Where(a => IsSystemAssembly(a))
            .Select(a => a.Location)
            .Distinct();

        foreach (var path in coreAssemblies)
        {
            if (!assemblyPaths.Contains(path))
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(path));
                    assemblyPaths.Add(path);
                }
                catch
                {
                    // Skip problematic assemblies
                }
            }
        }

        // Cache to disk
        if (assemblyPaths.Count > 0)
        {
            _cache.CacheReferences(targetFramework, assemblyPaths);
        }

        return references;
    }

    /// <summary>
    /// Gets the list of runtime assemblies needed for a framework.
    /// </summary>
    private static string[] GetRuntimeAssemblies(string targetFramework)
    {
        return targetFramework.ToLowerInvariant() switch
        {
            "net8.0" or "net9.0" => new[]
            {
                "System.Runtime",
                "System.Collections",
                "System.Linq",
                "System.Console",
                "System.Private.CoreLib",
                "mscorlib",
                "netstandard"
            },
            "netstandard2.1" or "netstandard2.0" => new[]
            {
                "netstandard",
                "System.Runtime",
                "System.Collections",
                "System.Linq"
            },
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    /// Checks if an assembly is a system/runtime assembly.
    /// </summary>
    private static bool IsSystemAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name ?? string.Empty;
        return name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("System", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("netstandard", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public CacheStatistics GetCacheStatistics()
    {
        return _cache.GetStatistics();
    }

    /// <summary>
    /// Clears all caches (memory and disk).
    /// </summary>
    public void ClearAllCaches()
    {
        _memoryCache.Clear();
        _cache.ClearAllCache();
        _downloader.ClearCache();
    }

    /// <summary>
    /// Gets all supported framework monikers.
    /// </summary>
    public static IReadOnlySet<string> GetSupportedFrameworks()
    {
        return FrameworkMoniker.SupportedFrameworks;
    }

    /// <summary>
    /// Disposes resources used by the resolver.
    /// </summary>
    public void Dispose()
    {
        _downloader?.Dispose();
    }
}
