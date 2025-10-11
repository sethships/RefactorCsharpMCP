using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;

/// <summary>
/// Downloads NuGet packages and extracts reference assemblies.
/// Uses NuGet v3 protocol for efficient package downloads.
/// </summary>
public class NuGetPackageDownloader
{
    private readonly string _packagesDirectory;
    private readonly ILogger _logger;
    private readonly SourceCacheContext _cache;
    private readonly SourceRepository _repository;

    public NuGetPackageDownloader(string? packagesDirectory = null)
    {
        _packagesDirectory = packagesDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".refactor-csharp-mcp",
            "nuget-packages"
        );

        Directory.CreateDirectory(_packagesDirectory);

        _logger = NullLogger.Instance;
        _cache = new SourceCacheContext();

        // Use official NuGet.org source
        var packageSource = new PackageSource("https://api.nuget.org/v3/index.json");
        _repository = Repository.Factory.GetCoreV3(packageSource);
    }

    /// <summary>
    /// Downloads a NuGet package and extracts reference assemblies for a target framework.
    /// </summary>
    /// <param name="packageId">NuGet package ID (e.g., "Microsoft.NETFramework.ReferenceAssemblies.net48")</param>
    /// <param name="targetFramework">Target framework moniker (e.g., "net48")</param>
    /// <returns>Paths to extracted assembly files</returns>
    public async Task<IReadOnlyList<string>> DownloadAndExtractAsync(string packageId, string targetFramework)
    {
        try
        {
            // Find the package
            var findPackageResource = await _repository.GetResourceAsync<FindPackageByIdResource>();

            // Get all versions and use the latest
            var versions = await findPackageResource.GetAllVersionsAsync(
                packageId,
                _cache,
                _logger,
                CancellationToken.None
            );

            var latestVersion = versions
                .Where(v => !v.IsPrerelease)
                .OrderByDescending(v => v)
                .FirstOrDefault();

            if (latestVersion == null)
            {
                throw new InvalidOperationException($"No stable version found for package: {packageId}");
            }

            // Download the package
            var packagePath = Path.Combine(_packagesDirectory, $"{packageId}.{latestVersion}.nupkg");

            if (!File.Exists(packagePath))
            {
                using var packageStream = File.Create(packagePath);
                var success = await findPackageResource.CopyNupkgToStreamAsync(
                    packageId,
                    latestVersion,
                    packageStream,
                    _cache,
                    _logger,
                    CancellationToken.None
                );

                if (!success)
                {
                    throw new InvalidOperationException($"Failed to download package: {packageId}");
                }
            }

            // Extract reference assemblies
            return ExtractReferenceAssemblies(packagePath, targetFramework, latestVersion.ToString());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to download/extract package {packageId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts reference assemblies from a downloaded .nupkg file.
    /// </summary>
    private List<string> ExtractReferenceAssemblies(string packagePath, string targetFramework, string version)
    {
        var extractedAssemblies = new List<string>();
        var extractDir = Path.Combine(
            _packagesDirectory,
            "extracted",
            Path.GetFileNameWithoutExtension(packagePath)
        );

        Directory.CreateDirectory(extractDir);

        using var packageReader = new PackageArchiveReader(packagePath);
        var packageFiles = packageReader.GetFiles();

        // Reference assemblies are typically in:
        // - ref/net48/ (for .NET Framework reference assemblies)
        // - lib/net48/ (fallback)
        // - build/net48/ (sometimes)

        var refPaths = new[] { $"ref/{targetFramework}/", $"lib/{targetFramework}/", $"build/{targetFramework}/" };

        foreach (var file in packageFiles)
        {
            // Check if file is in reference assembly directory and is a .dll
            if (refPaths.Any(p => file.StartsWith(p, StringComparison.OrdinalIgnoreCase)) &&
                file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = Path.GetFileName(file);
                var destinationPath = Path.Combine(extractDir, fileName);

                // Extract the file
                using var sourceStream = packageReader.GetStream(file);
                using var destinationStream = File.Create(destinationPath);
                sourceStream.CopyTo(destinationStream);

                extractedAssemblies.Add(destinationPath);
            }
        }

        // If no assemblies found in ref/ or lib/, look for any .dll files
        if (extractedAssemblies.Count == 0)
        {
            foreach (var file in packageFiles.Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                var fileName = Path.GetFileName(file);
                var destinationPath = Path.Combine(extractDir, fileName);

                using var sourceStream = packageReader.GetStream(file);
                using var destinationStream = File.Create(destinationPath);
                sourceStream.CopyTo(destinationStream);

                extractedAssemblies.Add(destinationPath);
            }
        }

        return extractedAssemblies;
    }

    /// <summary>
    /// Checks if a package is already downloaded.
    /// </summary>
    public bool IsPackageDownloaded(string packageId)
    {
        var packageFiles = Directory.GetFiles(_packagesDirectory, $"{packageId}.*.nupkg");
        return packageFiles.Length > 0;
    }

    /// <summary>
    /// Clears downloaded packages cache.
    /// </summary>
    public void ClearCache()
    {
        if (Directory.Exists(_packagesDirectory))
        {
            Directory.Delete(_packagesDirectory, recursive: true);
            Directory.CreateDirectory(_packagesDirectory);
        }
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }
}
