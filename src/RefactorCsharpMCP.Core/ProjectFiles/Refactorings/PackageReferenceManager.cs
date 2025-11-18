using System.Diagnostics;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using RefactorCsharpMCP.Core.ProjectFiles.Infrastructure;
using RefactorCsharpMCP.Core.ProjectFiles.Models;
using RefactorCsharpMCP.Core.ProjectFiles.NuGet;
using RefactorCsharpMCP.Core.Refactorings;
using RefactorCsharpMCP.Core.Validation;

namespace RefactorCsharpMCP.Core.ProjectFiles.Refactorings;

/// <summary>
/// Manages NuGet package references in .csproj files.
/// Supports add, update, and remove operations with framework compatibility validation.
/// </summary>
public class PackageReferenceManager : ProjectRefactoringBase
{
    public PackageReferenceManager(
        ILogger? logger = null,
        NuGetClientWrapper? nugetClient = null)
        : base(logger, nugetClient)
    {
    }

    /// <summary>
    /// Manages a package reference (add, update, or remove) in a project or solution.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file or solution directory.</param>
    /// <param name="operation">The operation to perform (Add, Update, Remove).</param>
    /// <param name="packageId">The NuGet package identifier.</param>
    /// <param name="version">The package version (required for Add/Update).</param>
    /// <param name="options">Refactoring options.</param>
    /// <param name="targetFramework">Optional target framework for compatibility validation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Refactoring result with success/failure status.</returns>
    public async Task<RefactoringResult> ManagePackageReferenceAsync(
        string projectPath,
        PackageOperation operation,
        string packageId,
        string? version = null,
        ProjectRefactoringOptions? options = null,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        options ??= ProjectRefactoringOptions.Default;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Input validation
            CurrentPhase = "Input Validation";
            var validationResult = ValidateInputs(projectPath, operation, packageId, version);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            // Determine if we're working with a single project or solution
            var projectPaths = await DiscoverProjectsAsync(projectPath, options, cancellationToken);
            if (!projectPaths.Any())
            {
                return RefactoringResult.Failure(ErrorCode.PROJECT_NOT_FOUND, $"No projects found at: {projectPath}");
            }

            // For dry-run mode, preview changes
            if (options.DryRun)
            {
                return await PreviewChangesAsync(projectPaths, operation, packageId, version, cancellationToken);
            }

            // Execute the operation
            if (projectPaths.Count == 1)
            {
                return await ExecuteSingleProjectAsync(
                    projectPaths[0],
                    operation,
                    packageId,
                    version,
                    targetFramework,
                    options,
                    cancellationToken);
            }
            else
            {
                return await ExecuteBatchOperationAsync(
                    projectPaths,
                    operation,
                    packageId,
                    version,
                    targetFramework,
                    options,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger?.LogError(ex, "Package reference management failed");
            return RefactoringResult.Failure(ErrorCode.REFACTORING_FAILED, $"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes the operation on a single project.
    /// </summary>
    private async Task<RefactoringResult> ExecuteSingleProjectAsync(
        string projectPath,
        PackageOperation operation,
        string packageId,
        string? version,
        string? targetFramework,
        ProjectRefactoringOptions options,
        CancellationToken cancellationToken)
    {
        CurrentPhase = "Single Project Operation";

        // Create backup
        var backupPath = CreateBackup(projectPath);

        try
        {
            // Load project
            var context = FileLoader.LoadProjectContext(projectPath, options.PreserveFormatting);

            // Validate framework compatibility if adding/updating
            if (operation != PackageOperation.Remove && targetFramework != null)
            {
                var compatible = await ValidateFrameworkCompatibilityAsync(
                    packageId,
                    version!,
                    targetFramework,
                    cancellationToken);

                if (!compatible)
                {
                    Rollback(projectPath);
                    return RefactoringResult.Failure(
                        $"Package {packageId} {version} is not compatible with {targetFramework}");
                }
            }

            // Perform operation
            var modified = operation switch
            {
                PackageOperation.Add => AddPackageReference(context.Document!, packageId, version!),
                PackageOperation.Update => UpdatePackageReference(context.Document!, packageId, version!),
                PackageOperation.Remove => RemovePackageReference(context.Document!, packageId),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };

            if (!modified)
            {
                var reason = operation == PackageOperation.Add
                    ? "Package already exists"
                    : operation == PackageOperation.Update
                        ? "Package not found or version unchanged"
                        : "Package not found";

                CleanupBackups(options.CreateBackup);
                return RefactoringResult.Failure(ErrorCode.REFACTORING_FAILED, reason);
            }

            // Save changes
            FileLoader.SaveProject(context.Document!, projectPath, options.PreserveFormatting);

            Logger?.LogInformation(
                "{Operation} package {PackageId} {Version} in {ProjectPath}",
                operation,
                packageId,
                version,
                projectPath);

            // Validate build
            var buildResult = await ValidateBuildWithRollbackAsync(projectPath, options);
            if (!buildResult.IsSuccess)
            {
                return buildResult;
            }

            // Cleanup backups
            CleanupBackups(options.CreateBackup);

            var message = operation switch
            {
                PackageOperation.Add => $"Added package {packageId} {version}",
                PackageOperation.Update => $"Updated package {packageId} to {version}",
                PackageOperation.Remove => $"Removed package {packageId}",
                _ => "Operation completed"
            };

            return RefactoringResult.Success(string.Empty, message);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to execute package operation on {ProjectPath}", projectPath);
            Rollback(projectPath);
            return RefactoringResult.Failure(ErrorCode.REFACTORING_FAILED, $"Operation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes the operation on multiple projects (batch mode).
    /// </summary>
    private async Task<RefactoringResult> ExecuteBatchOperationAsync(
        List<string> projectPaths,
        PackageOperation operation,
        string packageId,
        string? version,
        string? targetFramework,
        ProjectRefactoringOptions options,
        CancellationToken cancellationToken)
    {
        CurrentPhase = "Batch Operation";

        var result = new BatchOperationResult();
        var modifiedProjects = new List<string>();

        // Create backups for all projects
        var backups = CreateBackups(projectPaths);

        try
        {
            foreach (var projectPath in projectPaths)
            {
                try
                {
                    // Load project
                    var context = FileLoader.LoadProjectContext(projectPath, options.PreserveFormatting);

                    // Validate framework compatibility if adding/updating
                    if (operation != PackageOperation.Remove && targetFramework != null)
                    {
                        var compatible = await ValidateFrameworkCompatibilityAsync(
                            packageId,
                            version!,
                            targetFramework,
                            cancellationToken);

                        if (!compatible)
                        {
                            result.Skipped.Add((projectPath, $"Not compatible with {targetFramework}"));
                            continue;
                        }
                    }

                    // Perform operation
                    var modified = operation switch
                    {
                        PackageOperation.Add => AddPackageReference(context.Document!, packageId, version!),
                        PackageOperation.Update => UpdatePackageReference(context.Document!, packageId, version!),
                        PackageOperation.Remove => RemovePackageReference(context.Document!, packageId),
                        _ => throw new ArgumentException($"Unknown operation: {operation}")
                    };

                    if (!modified)
                    {
                        var reason = operation == PackageOperation.Add
                            ? "Package already exists"
                            : "Package not found";
                        result.Skipped.Add((projectPath, reason));
                        continue;
                    }

                    // Save changes
                    FileLoader.SaveProject(context.Document!, projectPath, options.PreserveFormatting);
                    modifiedProjects.Add(projectPath);
                    result.Succeeded.Add(projectPath);

                    Logger?.LogInformation(
                        "{Operation} package {PackageId} in {ProjectPath}",
                        operation,
                        packageId,
                        projectPath);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Failed to process {ProjectPath}", projectPath);
                    result.Failed[projectPath] = ex.Message;
                }
            }

            // If any projects failed, rollback all changes
            if (result.Failed.Any())
            {
                Logger?.LogWarning("Batch operation had failures, rolling back all changes");
                RollbackAll(modifiedProjects);
                result.RolledBack = true;

                CleanupBackups(options.CreateBackup);

                return RefactoringResult.Failure(
                    $"Batch operation failed: {result.Summary}\n\nErrors:\n{string.Join("\n", result.Failed.Select(f => $"- {f.Key}: {f.Value}"))}");
            }

            // Validate builds if requested
            if (options.ValidateBuild && modifiedProjects.Any())
            {
                var buildResult = await ValidateBuildsWithRollbackAsync(modifiedProjects, options);
                if (!buildResult.IsSuccess)
                {
                    CleanupBackups(options.CreateBackup);
                    return RefactoringResult.Failure(
                        $"Build validation failed: {buildResult.Summary}");
                }
            }

            // Cleanup backups
            CleanupBackups(options.CreateBackup);

            var message = operation switch
            {
                PackageOperation.Add => $"Added package {packageId} {version} to {result.Succeeded.Count} projects",
                PackageOperation.Update => $"Updated package {packageId} to {version} in {result.Succeeded.Count} projects",
                PackageOperation.Remove => $"Removed package {packageId} from {result.Succeeded.Count} projects",
                _ => $"Batch operation completed: {result.Summary}"
            };

            return RefactoringResult.Success(string.Empty, message);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Batch operation failed with exception");
            RollbackAll(modifiedProjects);
            CleanupBackups(options.CreateBackup);
            return RefactoringResult.Failure(ErrorCode.REFACTORING_FAILED, $"Batch operation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Previews changes in dry-run mode.
    /// </summary>
    private async Task<RefactoringResult> PreviewChangesAsync(
        List<string> projectPaths,
        PackageOperation operation,
        string packageId,
        string? version,
        CancellationToken cancellationToken)
    {
        var preview = new List<string>();

        foreach (var projectPath in projectPaths)
        {
            var context = FileLoader.LoadProjectContext(projectPath);
            var packageExists = context.PackageReferences.Any(p =>
                p.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase));

            var action = operation switch
            {
                PackageOperation.Add when packageExists => $"SKIP (already exists): {projectPath}",
                PackageOperation.Add => $"ADD {packageId} {version}: {projectPath}",
                PackageOperation.Update when packageExists => $"UPDATE {packageId} to {version}: {projectPath}",
                PackageOperation.Update => $"SKIP (not found): {projectPath}",
                PackageOperation.Remove when packageExists => $"REMOVE {packageId}: {projectPath}",
                PackageOperation.Remove => $"SKIP (not found): {projectPath}",
                _ => $"UNKNOWN: {projectPath}"
            };

            preview.Add(action);
        }

        var previewText = string.Join("\n", preview);
        return RefactoringResult.Success(string.Empty, $"DRY RUN Preview:\n{previewText}");
    }

    /// <summary>
    /// Validates inputs for the package management operation.
    /// </summary>
    private RefactoringResult ValidateInputs(
        string projectPath,
        PackageOperation operation,
        string packageId,
        string? version)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return RefactoringResult.Failure(ErrorCode.MISSING_PARAMETER, "Project path cannot be null or empty");
        }

        if (string.IsNullOrWhiteSpace(packageId))
        {
            return RefactoringResult.Failure(ErrorCode.MISSING_PARAMETER, "Package ID cannot be null or empty");
        }

        // Validate package ID format (alphanumeric, dots, hyphens, underscores)
        if (!IsValidPackageId(packageId))
        {
            return RefactoringResult.Failure(
                ErrorCode.INVALID_IDENTIFIER,
                $"Invalid package ID '{packageId}'. " +
                "Package IDs must contain only alphanumeric characters, dots, hyphens, and underscores.");
        }

        if ((operation == PackageOperation.Add || operation == PackageOperation.Update)
            && string.IsNullOrWhiteSpace(version))
        {
            return RefactoringResult.Failure(ErrorCode.MISSING_PARAMETER, $"Version is required for {operation} operation");
        }

        return RefactoringResult.Success(string.Empty, "Validation passed");
    }

    /// <summary>
    /// Discovers projects to operate on (single project or all projects in solution).
    /// </summary>
    private async Task<List<string>> DiscoverProjectsAsync(
        string projectPath,
        ProjectRefactoringOptions options,
        CancellationToken cancellationToken)
    {
        var projects = new List<string>();

        if (File.Exists(projectPath) && projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            projects.Add(projectPath);
        }
        else if (Directory.Exists(projectPath))
        {
            if (options.ApplyToAllProjects)
            {
                // Find all .csproj files in the directory tree
                projects.AddRange(Directory.GetFiles(projectPath, "*.csproj", SearchOption.AllDirectories));
            }
            else
            {
                // Find .csproj in the immediate directory
                projects.AddRange(Directory.GetFiles(projectPath, "*.csproj", SearchOption.TopDirectoryOnly));
            }
        }

        return projects;
    }

    /// <summary>
    /// Validates that a package version is compatible with a target framework.
    /// </summary>
    private async Task<bool> ValidateFrameworkCompatibilityAsync(
        string packageId,
        string version,
        string targetFramework,
        CancellationToken cancellationToken)
    {
        try
        {
            return await NuGetClient.IsCompatibleWithFrameworkAsync(
                packageId,
                version,
                targetFramework,
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to validate framework compatibility, allowing operation to proceed");
            return true; // Allow operation if validation fails
        }
    }

    /// <summary>
    /// Adds a package reference to the project XML document.
    /// Returns true if the package was added, false if it already exists.
    /// </summary>
    private bool AddPackageReference(XDocument document, string packageId, string version)
    {
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;

        // Check if package already exists
        var existingPackage = document.Descendants(ns + ProjectFileConstants.Elements.PackageReference)
            .FirstOrDefault(p => p.Attribute(ProjectFileConstants.Attributes.Include)?.Value
                .Equals(packageId, StringComparison.OrdinalIgnoreCase) == true);

        if (existingPackage != null)
        {
            Logger?.LogDebug("Package {PackageId} already exists", packageId);
            return false;
        }

        // Find or create ItemGroup for PackageReferences
        var itemGroup = document.Descendants(ns + ProjectFileConstants.Elements.ItemGroup)
            .FirstOrDefault(ig => ig.Elements(ns + ProjectFileConstants.Elements.PackageReference).Any());

        if (itemGroup == null)
        {
            itemGroup = new XElement(ns + ProjectFileConstants.Elements.ItemGroup);
            document.Root?.Add(itemGroup);
        }

        // Create new PackageReference element
        var packageRef = new XElement(
            ns + ProjectFileConstants.Elements.PackageReference,
            new XAttribute(ProjectFileConstants.Attributes.Include, packageId),
            new XAttribute(ProjectFileConstants.Attributes.Version, version)
        );

        itemGroup.Add(packageRef);

        Logger?.LogDebug("Added package {PackageId} {Version}", packageId, version);
        return true;
    }

    /// <summary>
    /// Updates a package reference version in the project XML document.
    /// Returns true if the package was updated, false if not found or version unchanged.
    /// </summary>
    private bool UpdatePackageReference(XDocument document, string packageId, string version)
    {
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;

        var packageRef = document.Descendants(ns + ProjectFileConstants.Elements.PackageReference)
            .FirstOrDefault(p => p.Attribute(ProjectFileConstants.Attributes.Include)?.Value
                .Equals(packageId, StringComparison.OrdinalIgnoreCase) == true);

        if (packageRef == null)
        {
            Logger?.LogDebug("Package {PackageId} not found for update", packageId);
            return false;
        }

        var currentVersion = packageRef.Attribute(ProjectFileConstants.Attributes.Version)?.Value;
        if (currentVersion == version)
        {
            Logger?.LogDebug("Package {PackageId} already at version {Version}", packageId, version);
            return false;
        }

        // Update version
        var versionAttr = packageRef.Attribute(ProjectFileConstants.Attributes.Version);
        if (versionAttr != null)
        {
            versionAttr.Value = version;
        }
        else
        {
            packageRef.Add(new XAttribute(ProjectFileConstants.Attributes.Version, version));
        }

        Logger?.LogDebug("Updated package {PackageId} from {OldVersion} to {NewVersion}", packageId, currentVersion, version);
        return true;
    }

    /// <summary>
    /// Removes a package reference from the project XML document.
    /// Returns true if the package was removed, false if not found.
    /// </summary>
    private bool RemovePackageReference(XDocument document, string packageId)
    {
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;

        var packageRef = document.Descendants(ns + ProjectFileConstants.Elements.PackageReference)
            .FirstOrDefault(p => p.Attribute(ProjectFileConstants.Attributes.Include)?.Value
                .Equals(packageId, StringComparison.OrdinalIgnoreCase) == true);

        if (packageRef == null)
        {
            Logger?.LogDebug("Package {PackageId} not found for removal", packageId);
            return false;
        }

        // Remove the package reference
        packageRef.Remove();

        // Clean up empty ItemGroup
        var itemGroup = document.Descendants(ns + ProjectFileConstants.Elements.ItemGroup)
            .FirstOrDefault(ig => !ig.Elements().Any());

        if (itemGroup != null)
        {
            itemGroup.Remove();
        }

        Logger?.LogDebug("Removed package {PackageId}", packageId);
        return true;
    }
}
