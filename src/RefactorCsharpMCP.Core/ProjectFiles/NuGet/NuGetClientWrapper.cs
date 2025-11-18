using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace RefactorCsharpMCP.Core.ProjectFiles.NuGet;

/// <summary>
/// Wrapper around NuGet client APIs for package metadata retrieval and compatibility checking.
/// Provides caching for improved performance.
/// </summary>
public class NuGetClientWrapper : IDisposable
{
    private bool _disposed;
    private readonly ILogger<NuGetClientWrapper> _logger;
    private readonly SourceCacheContext _cache;
    private readonly SourceRepository _sourceRepository;
    private readonly ConcurrentDictionary<string, PackageMetadataCache> _metadataCache = new();

    public NuGetClientWrapper(ILogger<NuGetClientWrapper>? logger = null, string? sourceUrl = null)
    {
        _logger = logger ?? NullLogger<NuGetClientWrapper>.Instance;
        _cache = new SourceCacheContext();

        // Use nuget.org by default
        var packageSource = new PackageSource(sourceUrl ?? "https://api.nuget.org/v3/index.json");
        _sourceRepository = Repository.Factory.GetCoreV3(packageSource);

        _logger.LogDebug("Initialized NuGet client with source: {Source}", packageSource.Source);
    }

    /// <summary>
    /// Gets metadata for a specific package version.
    /// Results are cached for performance.
    /// </summary>
    /// <param name="packageId">The NuGet package identifier.</param>
    /// <param name="version">The package version (optional - latest stable if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="timeoutSeconds">Optional timeout override for this operation. If not specified, uses the instance timeout.</param>
    /// <returns>Package metadata or null if not found.</returns>
    public async Task<PackageMetadata?> GetPackageMetadataAsync(
        string packageId,
        string? version = null,
        CancellationToken cancellationToken = default,
        int? timeoutSeconds = null)
    {
        var cacheKey = $"{packageId}|{version ?? "latest"}";

        if (_metadataCache.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
        {
            _logger.LogDebug("Cache hit for package: {PackageId} {Version}", packageId, version);
            return cached.Metadata;
        }

        // Create timeout cancellation token
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds ?? _timeoutSeconds));

        try
        {
            var resource = await _sourceRepository.GetResourceAsync<PackageMetadataResource>(cancellationToken);

            IPackageSearchMetadata? metadata;

            if (string.IsNullOrWhiteSpace(version))
            {
                // Get latest stable version
                metadata = await GetLatestStableMetadataAsync(resource, packageId, cancellationToken);
            }
            else
            {
                // Get specific version
                var nugetVersion = NuGetVersion.Parse(version);
                metadata = await resource.GetMetadataAsync(
                    new PackageIdentity(packageId, nugetVersion),
                    _cache,
                    NullLogger.Instance,
                    cancellationToken);
            }

            if (metadata == null)
            {
                _logger.LogWarning("Package not found: {PackageId} {Version}", packageId, version);
                return null;
            }

            var packageMetadata = new PackageMetadata
            {
                PackageId = metadata.Identity.Id,
                Version = metadata.Identity.Version.ToString(),
                Description = metadata.Description,
                Authors = metadata.Authors,
                DependencySets = metadata.DependencySets
                    .Select(ds => new DependencySet
                    {
                        TargetFramework = ds.TargetFramework.GetShortFolderName(),
                        Dependencies = ds.Packages
                            .Select(p => new PackageDependency
                            {
                                Id = p.Id,
                                VersionRange = p.VersionRange.ToString()
                            })
                            .ToList()
                    })
                    .ToList()
            };

            // Cache the result
            _metadataCache[cacheKey] = new PackageMetadataCache
            {
                Metadata = packageMetadata,
                CachedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Retrieved metadata for package: {PackageId} {Version}", packageId, version);

            return packageMetadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metadata for package: {PackageId} {Version}", packageId, version);
            return null;
        }
    }

    /// <summary>
    /// Checks if a package version is compatible with a target framework.
    /// </summary>
    /// <param name="packageId">The NuGet package identifier.</param>
    /// <param name="version">The package version.</param>
    /// <param name="targetFramework">The target framework moniker (e.g., "net8.0").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if compatible, false otherwise.</returns>
    public async Task<bool> IsCompatibleWithFrameworkAsync(
        string packageId,
        string version,
        string targetFramework,
        CancellationToken cancellationToken = default)
    {
        var metadata = await GetPackageMetadataAsync(packageId, version, cancellationToken);
        if (metadata == null)
        {
            return false;
        }

        if (!metadata.DependencySets.Any())
        {
            // No framework-specific dependencies, assume compatible
            return true;
        }

        var framework = NuGetFramework.Parse(targetFramework);

        // Check if any dependency set is compatible with the target framework
        foreach (var dependencySet in metadata.DependencySets)
        {
            var depFramework = NuGetFramework.Parse(dependencySet.TargetFramework);

            // Use NuGet's compatibility check
            if (DefaultCompatibilityProvider.Instance.IsCompatible(framework, depFramework))
            {
                _logger.LogDebug(
                    "Package {PackageId} {Version} is compatible with {TargetFramework}",
                    packageId,
                    version,
                    targetFramework);
                return true;
            }
        }

        _logger.LogWarning(
            "Package {PackageId} {Version} is NOT compatible with {TargetFramework}",
            packageId,
            version,
            targetFramework);

        return false;
    }

    /// <summary>
    /// Gets the latest stable version of a package.
    /// </summary>
    /// <param name="packageId">The NuGet package identifier.</param>
    /// <param name="includePrerelease">Whether to include prerelease versions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest version string, or null if not found.</returns>
    public async Task<string?> GetLatestVersionAsync(
        string packageId,
        bool includePrerelease = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resource = await _sourceRepository.GetResourceAsync<PackageMetadataResource>(cancellationToken);

            var metadata = includePrerelease
                ? await GetLatestMetadataAsync(resource, packageId, cancellationToken)
                : await GetLatestStableMetadataAsync(resource, packageId, cancellationToken);

            if (metadata == null)
            {
                return null;
            }

            var version = metadata.Identity.Version.ToString();
            _logger.LogInformation("Latest version of {PackageId}: {Version}", packageId, version);

            return version;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest version for package: {PackageId}", packageId);
            return null;
        }
    }

    /// <summary>
    /// Clears the metadata cache.
    /// </summary>
    public void ClearCache()
    {
        _metadataCache.Clear();
        _logger.LogDebug("Metadata cache cleared");
    }

    /// <summary>
    /// Gets the latest stable metadata for a package.
    /// </summary>
    private async Task<IPackageSearchMetadata?> GetLatestStableMetadataAsync(
        PackageMetadataResource resource,
        string packageId,
        CancellationToken cancellationToken)
    {
        var metadata = await resource.GetMetadataAsync(
            packageId,
            includePrerelease: false,
            includeUnlisted: false,
            _cache,
            NullLogger.Instance,
            cancellationToken);

        return metadata
            .Where(m => !m.Identity.Version.IsPrerelease)
            .OrderByDescending(m => m.Identity.Version)
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets the latest metadata (including prerelease) for a package.
    /// </summary>
    private async Task<IPackageSearchMetadata?> GetLatestMetadataAsync(
        PackageMetadataResource resource,
        string packageId,
        CancellationToken cancellationToken)
    {
        var metadata = await resource.GetMetadataAsync(
            packageId,
            includePrerelease: true,
            includeUnlisted: false,
            _cache,
            NullLogger.Instance,
            cancellationToken);

        return metadata
            .OrderByDescending(m => m.Identity.Version)
            .FirstOrDefault();
    }

    /// <summary>
    /// Disposes resources used by the NuGetClientWrapper.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected dispose pattern implementation.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _cache?.Dispose();
                _logger.LogDebug("NuGetClientWrapper disposed");
            }

            _disposed = true;
        }
    }
}

/// <summary>
/// Cached package metadata with expiration.
/// </summary>
internal class PackageMetadataCache
{
    public PackageMetadata? Metadata { get; set; }
    public DateTime CachedAt { get; set; }

    /// <summary>
    /// Cache expires after 1 hour.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow - CachedAt > TimeSpan.FromHours(1);
}

/// <summary>
/// Simplified package metadata model.
/// </summary>
public class PackageMetadata
{
    public required string PackageId { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public string? Authors { get; init; }
    public List<DependencySet> DependencySets { get; init; } = new();
}

/// <summary>
/// Represents a dependency set for a specific target framework.
/// </summary>
public class DependencySet
{
    public required string TargetFramework { get; init; }
    public List<PackageDependency> Dependencies { get; init; } = new();
}

/// <summary>
/// Represents a package dependency.
/// </summary>
public class PackageDependency
{
    public required string Id { get; init; }
    public required string VersionRange { get; init; }
}
