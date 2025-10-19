using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.Reflection.PortableExecutable;

namespace RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;

/// <summary>
/// Downloads NuGet packages and extracts reference assemblies.
/// Uses NuGet v3 protocol for efficient package downloads.
/// </summary>
public class NuGetPackageDownloader : IDisposable
{
    private readonly string _packagesDirectory;
    private readonly NuGet.Common.ILogger _nugetLogger;
    private readonly SourceCacheContext _cache;
    private readonly SourceRepository _repository;
    private readonly Microsoft.Extensions.Logging.ILogger? _logger;

    public NuGetPackageDownloader(string? packagesDirectory = null, Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        _logger = logger;
        _packagesDirectory = packagesDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".refactor-csharp-mcp",
            "nuget-packages"
        );

        Directory.CreateDirectory(_packagesDirectory);

        _nugetLogger = NullLogger.Instance;
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
                _nugetLogger,
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

            _logger?.LogInformation("Downloading NuGet package {PackageId} version {Version} to {Path}",
                packageId, latestVersion, packagePath);

            if (!File.Exists(packagePath))
            {
                // Use explicit using block and flush for Windows compatibility
                using (var packageStream = File.Create(packagePath))
                {
                    var success = await findPackageResource.CopyNupkgToStreamAsync(
                        packageId,
                        latestVersion,
                        packageStream,
                        _cache,
                        _nugetLogger,
                        CancellationToken.None
                    );

                    if (!success)
                    {
                        throw new InvalidOperationException($"Failed to download package: {packageId}");
                    }

                    // Explicitly flush to ensure data is written to disk (Windows requirement)
                    await packageStream.FlushAsync();
                } // Stream disposed here, ensuring file handle is released

                // Verify file was actually written to disk
                if (!File.Exists(packagePath))
                {
                    throw new InvalidOperationException($"Package file was not created: {packagePath}");
                }

                var fileInfo = new FileInfo(packagePath);
                if (fileInfo.Length == 0)
                {
                    throw new InvalidOperationException($"Package file is empty: {packagePath}");
                }

                _logger?.LogInformation("Downloaded {PackageId} successfully ({Bytes} bytes)",
                    packageId, fileInfo.Length);
            }
            else
            {
                _logger?.LogInformation("Package {PackageId} already exists at {Path}",
                    packageId, packagePath);
            }

            // Extract reference assemblies
            var result = ExtractReferenceAssemblies(packagePath, targetFramework, latestVersion.ToString());
            _logger?.LogInformation("Extraction complete: {Count} assemblies extracted for {TargetFramework}",
                result.Count, targetFramework);
            return result;
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
        var packageFiles = packageReader.GetFiles().ToList();

        _logger?.LogDebug("Package contains {Count} total files", packageFiles.Count);
        if (packageFiles.Count > 0)
        {
            var dllFiles = packageFiles.Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();
            _logger?.LogDebug("Found {DllCount} DLL files in package", dllFiles.Count);
            foreach (var dll in dllFiles.Take(5))
            {
                _logger?.LogDebug("  DLL: {Path}", dll);
            }
        }

        // Reference assemblies are typically in:
        // - ref/net48/ (for .NET Framework reference assemblies)
        // - lib/net48/ (fallback)
        // - build/net48/ (sometimes)
        // - build/.NETFramework/v4.8/ (Microsoft.NETFramework.ReferenceAssemblies structure)

        // Map targetFramework to .NETFramework version string
        var dotNetFrameworkVersion = targetFramework switch
        {
            "net481" => "v4.8.1",
            "net48" => "v4.8",
            "net472" => "v4.7.2",
            "net471" => "v4.7.1",
            "net47" => "v4.7",
            "net462" => "v4.6.2",
            "net35" => "v3.5",
            _ => null
        };

        var refPaths = new List<string>
        {
            $"ref/{targetFramework}/",
            $"lib/{targetFramework}/",
            $"build/{targetFramework}/"
        };

        // Add .NETFramework-style paths if applicable (including Facades subdirectory)
        if (dotNetFrameworkVersion != null)
        {
            refPaths.Add($"build/.NETFramework/{dotNetFrameworkVersion}/");
            refPaths.Add($"build/.NETFramework/{dotNetFrameworkVersion}/Facades/");
            refPaths.Add($"ref/.NETFramework/{dotNetFrameworkVersion}/");
            refPaths.Add($"ref/.NETFramework/{dotNetFrameworkVersion}/Facades/");
        }

        foreach (var file in packageFiles)
        {
            if (!file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                continue;

            // Check if file is in reference assembly directory
            // Use path-agnostic matching (works with both / and \ separators)
            var normalizedFile = file.Replace('\\', '/');

            bool isInRefPath = false;
            foreach (var refPath in refPaths)
            {
                // Case-insensitive match for the path
                if (normalizedFile.StartsWith(refPath, StringComparison.OrdinalIgnoreCase))
                {
                    isInRefPath = true;
                    break;
                }
            }

            if (!isInRefPath)
                continue;

            var fileName = Path.GetFileName(file);
            if (string.IsNullOrEmpty(fileName))
            {
                _logger?.LogWarning("Skipping file with invalid path: {FilePath}", file);
                continue;
            }

            var destinationPath = Path.Combine(extractDir, fileName);

            // Skip if already extracted (avoid duplicates from multiple paths)
            if (File.Exists(destinationPath))
            {
                // If file exists, still add it to the list if it's a managed assembly
                if (IsManagedAssembly(destinationPath))
                {
                    extractedAssemblies.Add(destinationPath);
                }
                continue;
            }

            // Extract the file with explicit flushing for Windows compatibility
            try
            {
                using (var sourceStream = packageReader.GetStream(file))
                using (var destinationStream = File.Create(destinationPath))
                {
                    sourceStream.CopyTo(destinationStream);
                    // Explicitly flush to ensure data is written to disk (Windows requirement)
                    destinationStream.Flush();
                } // Streams disposed here, ensuring file handles are released

                // Verify file was actually written
                if (!File.Exists(destinationPath))
                {
                    _logger?.LogWarning("File was not created after extraction: {FilePath}", destinationPath);
                    continue;
                }

                var fileInfo = new FileInfo(destinationPath);
                if (fileInfo.Length == 0)
                {
                    _logger?.LogWarning("Extracted file is empty: {FilePath}", destinationPath);
                    continue;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to extract {FilePath}", file);
                continue;
            }

            // Validate that the DLL is a valid managed assembly for use as a reference
            if (!IsManagedAssembly(destinationPath))
            {
                _logger?.LogDebug("Skipping unmanaged/problematic assembly as reference: {FileName}", fileName);
                // Don't delete the file - leave it for transitive dependency resolution
                // But don't add it to the reference list
                continue;
            }

            extractedAssemblies.Add(destinationPath);
        }

        _logger?.LogInformation("Extracted {Count} assemblies for {TargetFramework} from standard paths",
            extractedAssemblies.Count, targetFramework);

        // For .NET Framework packages, if no assemblies found with specific paths,
        // fall back to extracting ALL DLLs from the package (IsManagedAssembly will filter)
        if (extractedAssemblies.Count == 0 && dotNetFrameworkVersion != null)
        {
            _logger?.LogInformation("No assemblies found in standard paths for {TargetFramework}, extracting all DLLs from package",
                targetFramework);

            _logger?.LogDebug("Searched paths: {Paths}", string.Join(", ", refPaths));

            var sampleFiles = packageFiles.Take(10).ToList();
            _logger?.LogDebug("Sample files in package ({Total} total): {SampleFiles}",
                packageFiles.Count(), string.Join(", ", sampleFiles));

            foreach (var file in packageFiles.Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                var fileName = Path.GetFileName(file);
                if (string.IsNullOrEmpty(fileName))
                {
                    _logger?.LogWarning("Skipping file with invalid path: {FilePath}", file);
                    continue;
                }

                var destinationPath = Path.Combine(extractDir, fileName);

                // Skip if file already exists (already extracted in previous section)
                if (File.Exists(destinationPath))
                {
                    // Still add to list if it's a managed assembly
                    if (IsManagedAssembly(destinationPath))
                    {
                        extractedAssemblies.Add(destinationPath);
                    }
                    continue;
                }

                // Extract with explicit flushing for Windows
                try
                {
                    using (var sourceStream = packageReader.GetStream(file))
                    using (var destinationStream = File.Create(destinationPath))
                    {
                        sourceStream.CopyTo(destinationStream);
                        destinationStream.Flush();
                    }

                    // Verify file was written
                    if (!File.Exists(destinationPath) || new FileInfo(destinationPath).Length == 0)
                    {
                        _logger?.LogWarning("Fallback extraction failed for {FileName}", fileName);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to extract {FileName} in fallback", fileName);
                    continue;
                }

                // Validate that the DLL is a valid managed assembly for use as a reference
                if (!IsManagedAssembly(destinationPath))
                {
                    _logger?.LogDebug("Skipping unmanaged/problematic assembly as reference: {FileName}", fileName);
                    // Don't delete the file - leave it for transitive dependency resolution
                    // But don't add it to the reference list
                    continue;
                }

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
    /// Clears downloaded packages cache (synchronous version).
    /// Uses retry logic to handle file locks (e.g., assemblies loaded by concurrent tests).
    /// </summary>
    public void ClearCache()
    {
        ClearCacheAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Clears downloaded packages cache (async version).
    /// Uses retry logic to handle file locks (e.g., assemblies loaded by concurrent tests).
    /// </summary>
    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(_packagesDirectory))
        {
            await FileSystemRetryHelper.SafeDeleteDirectoryAsync(_packagesDirectory, _logger, cancellationToken: cancellationToken);
            Directory.CreateDirectory(_packagesDirectory);
        }
    }


    /// <summary>
    /// Checks if a DLL file is a valid managed .NET assembly that can be used by Roslyn.
    /// Filters out known problematic assemblies (COM interop wrappers, unmanaged DLLs).
    /// </summary>
    private static bool IsManagedAssembly(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        // Filter out known unmanaged/problematic assemblies
        var problematicAssemblies = new[]
        {
            "System.EnterpriseServices.Wrapper.dll",  // COM interop wrapper - not a valid reference assembly
            "System.EnterpriseServices.Thunk.dll"      // Native thunk DLL - not managed
        };

        if (problematicAssemblies.Any(name => fileName.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            using var peReader = new PEReader(stream);

            // Check if it has a CLI header (managed code indicator)
            return peReader.HasMetadata && peReader.PEHeaders.CorHeader != null;
        }
        catch
        {
            // If we can't read the PE headers, it's not a valid managed assembly
            return false;
        }
    }

    public void Dispose()
    {
        _cache?.Dispose();
        // SourceRepository does not implement IDisposable, so no disposal needed
    }
}
