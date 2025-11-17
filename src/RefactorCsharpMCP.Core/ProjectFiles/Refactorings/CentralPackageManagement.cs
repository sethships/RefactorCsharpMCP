using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using RefactorCsharpMCP.Core.ProjectFiles.Infrastructure;
using RefactorCsharpMCP.Core.ProjectFiles.Models;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Core.ProjectFiles.Refactorings;

/// <summary>
/// Enables Central Package Management (CPM) for a solution.
/// Extracts package versions from all projects, resolves conflicts, and creates Directory.*.props files.
/// </summary>
public class CentralPackageManagement : ProjectRefactoringBase
{
    public CentralPackageManagement(ILogger? logger = null)
        : base(logger)
    {
    }

    /// <summary>
    /// Enables Central Package Management for a solution.
    /// </summary>
    /// <param name="solutionPath">Path to the solution directory or .sln file.</param>
    /// <param name="conflictStrategy">Strategy for resolving version conflicts.</param>
    /// <param name="options">Refactoring options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Refactoring result with success/failure status.</returns>
    public async Task<RefactoringResult> EnableCpmAsync(
        string solutionPath,
        ConflictResolutionStrategy conflictStrategy = ConflictResolutionStrategy.Fail,
        ProjectRefactoringOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= ProjectRefactoringOptions.Default;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate solution path
            CurrentPhase = "Solution Validation";
            var solutionDir = DetermineSolutionDirectory(solutionPath);
            if (solutionDir == null)
            {
                return RefactoringResult.Failure($"Invalid solution path: {solutionPath}");
            }

            // Discover all projects in solution
            CurrentPhase = "Project Discovery";
            var projectPaths = DiscoverProjects(solutionDir);
            if (!projectPaths.Any())
            {
                return RefactoringResult.Failure($"No projects found in: {solutionDir}");
            }

            Logger?.LogInformation("Found {Count} projects in solution", projectPaths.Count);

            // Check if CPM is already enabled
            var directoryPackagesProps = Path.Combine(solutionDir, ProjectFileConstants.FileNames.DirectoryPackagesProps);
            if (File.Exists(directoryPackagesProps) && !options.DryRun)
            {
                return RefactoringResult.Failure(
                    $"Central Package Management already enabled (Directory.Packages.props exists)");
            }

            // Extract package versions from all projects
            CurrentPhase = "Package Version Extraction";
            var packageVersions = ExtractPackageVersions(projectPaths);

            if (!packageVersions.Any())
            {
                return RefactoringResult.Failure("No package references found in solution");
            }

            Logger?.LogInformation("Found {Count} unique packages", packageVersions.Count);

            // Detect version conflicts
            CurrentPhase = "Conflict Detection";
            var conflicts = DetectConflicts(packageVersions);

            // For dry-run mode, show preview
            if (options.DryRun)
            {
                return PreviewCpm(solutionDir, packageVersions, conflicts, conflictStrategy);
            }

            // Resolve conflicts
            CurrentPhase = "Conflict Resolution";
            Dictionary<string, string> resolvedVersions;
            try
            {
                resolvedVersions = ResolveVersionConflicts(packageVersions, conflicts, conflictStrategy);
            }
            catch (InvalidOperationException ex)
            {
                return RefactoringResult.Failure($"Conflict resolution failed: {ex.Message}");
            }

            Logger?.LogInformation("Resolved {Count} package versions", resolvedVersions.Count);

            // Create backups for all projects
            CurrentPhase = "Backup Creation";
            var filesToBackup = projectPaths.ToList();
            var backups = CreateBackups(filesToBackup);

            try
            {
                // Create Directory.Build.props
                CurrentPhase = "Directory.Build.props Creation";
                var directoryBuildProps = Path.Combine(solutionDir, ProjectFileConstants.FileNames.DirectoryBuildProps);
                CreateDirectoryBuildProps(directoryBuildProps);

                // Create Directory.Packages.props
                CurrentPhase = "Directory.Packages.props Creation";
                CreateDirectoryPackagesProps(directoryPackagesProps, resolvedVersions);

                // Update all project files (remove Version attributes)
                CurrentPhase = "Project Update";
                UpdateProjects(projectPaths);

                Logger?.LogInformation("Enabled CPM for solution: {SolutionDir}", solutionDir);

                // Validate builds
                var buildResult = await ValidateBuildsWithRollbackAsync(projectPaths, options);
                if (!buildResult.IsSuccess)
                {
                    // Rollback also includes deleting the created Directory.*.props files
                    DeleteFile(directoryBuildProps);
                    DeleteFile(directoryPackagesProps);

                    return RefactoringResult.Failure($"Build validation failed: {buildResult.Summary}");
                }

                // Cleanup backups
                CleanupBackups(options.CreateBackup);

                var conflictMessage = conflicts.Any()
                    ? $"\n\nResolved {conflicts.Count} version conflicts using '{conflictStrategy}' strategy"
                    : "";

                return RefactoringResult.Success(
                    string.Empty,
                    $"Successfully enabled Central Package Management for {projectPaths.Count} projects{conflictMessage}");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "CPM enablement failed");
                RollbackAll(filesToBackup);

                // Delete created files
                DeleteFile(Path.Combine(solutionDir, ProjectFileConstants.FileNames.DirectoryBuildProps));
                DeleteFile(directoryPackagesProps);

                return RefactoringResult.Failure($"CPM enablement failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger?.LogError(ex, "CPM enablement failed with exception");
            return RefactoringResult.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Previews CPM enablement in dry-run mode.
    /// </summary>
    private RefactoringResult PreviewCpm(
        string solutionDir,
        Dictionary<string, List<PackageVersionInfo>> packageVersions,
        List<string> conflicts,
        ConflictResolutionStrategy strategy)
    {
        var preview = new StringBuilder();
        preview.AppendLine("DRY RUN - Central Package Management Preview:");
        preview.AppendLine();
        preview.AppendLine($"Solution Directory: {solutionDir}");
        preview.AppendLine($"Packages: {packageVersions.Count}");
        preview.AppendLine($"Conflicts: {conflicts.Count}");
        preview.AppendLine($"Conflict Strategy: {strategy}");
        preview.AppendLine();
        preview.AppendLine("Changes:");
        preview.AppendLine($"- Create {ProjectFileConstants.FileNames.DirectoryBuildProps}");
        preview.AppendLine($"- Create {ProjectFileConstants.FileNames.DirectoryPackagesProps} with package versions");
        preview.AppendLine("- Remove Version attributes from all PackageReferences");
        preview.AppendLine();

        if (conflicts.Any())
        {
            preview.AppendLine("⚠️ Version Conflicts Detected:");
            foreach (var packageId in conflicts.Take(10)) // Show first 10
            {
                var versions = packageVersions[packageId];
                preview.AppendLine($"  {packageId}: {string.Join(", ", versions.Select(v => v.Version).Distinct())}");
            }

            if (conflicts.Count > 10)
            {
                preview.AppendLine($"  ... and {conflicts.Count - 10} more");
            }

            preview.AppendLine();
            preview.AppendLine($"Resolution: Will use '{strategy}' strategy");
        }

        return RefactoringResult.Success(string.Empty, preview.ToString());
    }

    /// <summary>
    /// Determines the solution directory from a path (either directory or .sln file).
    /// </summary>
    private string? DetermineSolutionDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            return Path.GetFullPath(path);
        }

        if (File.Exists(path) && path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(path);
        }

        return null;
    }

    /// <summary>
    /// Discovers all .csproj files in the solution directory.
    /// </summary>
    private List<string> DiscoverProjects(string solutionDir)
    {
        return Directory.GetFiles(solutionDir, "*.csproj", SearchOption.AllDirectories).ToList();
    }

    /// <summary>
    /// Extracts package versions from all projects.
    /// </summary>
    private Dictionary<string, List<PackageVersionInfo>> ExtractPackageVersions(List<string> projectPaths)
    {
        var packageVersions = new Dictionary<string, List<PackageVersionInfo>>(StringComparer.OrdinalIgnoreCase);

        foreach (var projectPath in projectPaths)
        {
            var context = FileLoader.LoadProjectContext(projectPath);

            foreach (var packageRef in context.PackageReferences)
            {
                if (string.IsNullOrWhiteSpace(packageRef.Version))
                {
                    continue; // Skip packages without version (already using CPM)
                }

                if (!packageVersions.ContainsKey(packageRef.PackageId))
                {
                    packageVersions[packageRef.PackageId] = new List<PackageVersionInfo>();
                }

                packageVersions[packageRef.PackageId].Add(new PackageVersionInfo
                {
                    Version = packageRef.Version,
                    ProjectPath = projectPath
                });
            }
        }

        return packageVersions;
    }

    /// <summary>
    /// Detects version conflicts (multiple versions of same package).
    /// </summary>
    private List<string> DetectConflicts(Dictionary<string, List<PackageVersionInfo>> packageVersions)
    {
        var conflicts = new List<string>();

        foreach (var (packageId, versions) in packageVersions)
        {
            var distinctVersions = versions.Select(v => v.Version).Distinct().Count();
            if (distinctVersions > 1)
            {
                conflicts.Add(packageId);
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Resolves version conflicts based on the specified strategy.
    /// </summary>
    private Dictionary<string, string> ResolveVersionConflicts(
        Dictionary<string, List<PackageVersionInfo>> packageVersions,
        List<string> conflicts,
        ConflictResolutionStrategy strategy)
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (packageId, versions) in packageVersions)
        {
            var distinctVersions = versions.Select(v => v.Version).Distinct().ToList();

            if (distinctVersions.Count == 1)
            {
                // No conflict
                resolved[packageId] = distinctVersions[0];
            }
            else
            {
                // Conflict - resolve based on strategy
                resolved[packageId] = strategy switch
                {
                    ConflictResolutionStrategy.Fail => throw new InvalidOperationException(
                        $"Version conflict for package '{packageId}': {string.Join(", ", distinctVersions)}"),
                    ConflictResolutionStrategy.UseHighest => ResolveHighest(distinctVersions),
                    ConflictResolutionStrategy.UseLowest => ResolveLowest(distinctVersions),
                    ConflictResolutionStrategy.UseMostCommon => ResolveMostCommon(versions),
                    _ => throw new ArgumentException($"Unknown strategy: {strategy}")
                };

                Logger?.LogInformation(
                    "Resolved conflict for {PackageId}: {Versions} → {ResolvedVersion}",
                    packageId,
                    string.Join(", ", distinctVersions),
                    resolved[packageId]);
            }
        }

        return resolved;
    }

    /// <summary>
    /// Resolves to the highest version using NuGet version comparison.
    /// </summary>
    private string ResolveHighest(List<string> versions)
    {
        var nugetVersions = versions.Select(NuGetVersion.Parse).ToList();
        return nugetVersions.Max()!.ToString();
    }

    /// <summary>
    /// Resolves to the lowest version using NuGet version comparison.
    /// </summary>
    private string ResolveLowest(List<string> versions)
    {
        var nugetVersions = versions.Select(NuGetVersion.Parse).ToList();
        return nugetVersions.Min()!.ToString();
    }

    /// <summary>
    /// Resolves to the most commonly occurring version.
    /// </summary>
    private string ResolveMostCommon(List<PackageVersionInfo> versions)
    {
        return versions
            .GroupBy(v => v.Version)
            .OrderByDescending(g => g.Count())
            .First()
            .Key;
    }

    /// <summary>
    /// Creates Directory.Build.props with ManagePackageVersionsCentrally enabled.
    /// </summary>
    private void CreateDirectoryBuildProps(string path)
    {
        // Only create if it doesn't exist (to avoid overwriting user customizations)
        if (File.Exists(path))
        {
            Logger?.LogInformation("Directory.Build.props already exists, checking for CPM property");

            var existing = XDocument.Load(path);
            var ns = existing.Root?.Name.Namespace ?? XNamespace.None;

            var cpmProperty = existing.Descendants(ns + ProjectFileConstants.Elements.ManagePackageVersionsCentrally)
                .FirstOrDefault();

            if (cpmProperty == null)
            {
                // Add CPM property to existing file
                var propertyGroup = existing.Descendants(ns + ProjectFileConstants.Elements.PropertyGroup)
                    .FirstOrDefault();

                if (propertyGroup == null)
                {
                    propertyGroup = new XElement("PropertyGroup");
                    existing.Root?.Add(propertyGroup);
                }

                propertyGroup.Add(new XElement("ManagePackageVersionsCentrally", "true"));
                existing.Save(path);
                Logger?.LogInformation("Added ManagePackageVersionsCentrally to existing Directory.Build.props");
            }

            return;
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("Project",
                new XElement("PropertyGroup",
                    new XElement("ManagePackageVersionsCentrally", "true"))));

        document.Save(path);
        Logger?.LogInformation("Created Directory.Build.props");
    }

    /// <summary>
    /// Creates Directory.Packages.props with all package versions.
    /// </summary>
    private void CreateDirectoryPackagesProps(string path, Dictionary<string, string> packageVersions)
    {
        var project = new XElement("Project");

        // Sort packages alphabetically for readability
        var sortedPackages = packageVersions.OrderBy(kv => kv.Key);

        var itemGroup = new XElement("ItemGroup");

        foreach (var (packageId, version) in sortedPackages)
        {
            var packageVersion = new XElement("PackageVersion",
                new XAttribute("Include", packageId),
                new XAttribute("Version", version));

            itemGroup.Add(packageVersion);
        }

        project.Add(itemGroup);

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            project);

        document.Save(path);
        Logger?.LogInformation("Created Directory.Packages.props with {Count} packages", packageVersions.Count);
    }

    /// <summary>
    /// Updates all project files to remove Version attributes from PackageReferences.
    /// </summary>
    private void UpdateProjects(List<string> projectPaths)
    {
        foreach (var projectPath in projectPaths)
        {
            var context = FileLoader.LoadProjectContext(projectPath);
            var document = context.Document!;
            var ns = document.Root?.Name.Namespace ?? XNamespace.None;

            var packageRefs = document.Descendants(ns + ProjectFileConstants.Elements.PackageReference);

            var modified = false;

            foreach (var packageRef in packageRefs)
            {
                var versionAttr = packageRef.Attribute(ProjectFileConstants.Attributes.Version);
                if (versionAttr != null)
                {
                    versionAttr.Remove();
                    modified = true;
                }
            }

            if (modified)
            {
                FileLoader.SaveProject(document, projectPath, preserveFormatting: true);
                Logger?.LogDebug("Updated project: {ProjectPath}", projectPath);
            }
        }
    }

    /// <summary>
    /// Safely deletes a file if it exists.
    /// </summary>
    private void DeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                Logger?.LogDebug("Deleted file: {Path}", path);
            }
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to delete file: {Path}", path);
        }
    }
}

/// <summary>
/// Package version information with source project.
/// </summary>
internal class PackageVersionInfo
{
    public required string Version { get; init; }
    public required string ProjectPath { get; init; }
}
