using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RefactorCsharpMCP.Core.ProjectFiles.Infrastructure;
using RefactorCsharpMCP.Core.ProjectFiles.Models;
using RefactorCsharpMCP.Core.ProjectFiles.NuGet;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Core.ProjectFiles.Refactorings;

/// <summary>
/// Base class for project file refactorings, extending RefactoringBase with project-file-specific functionality.
/// Provides common infrastructure for project file manipulation, backup/rollback, and build validation.
/// </summary>
public abstract class ProjectRefactoringBase : RefactoringBase, IDisposable
{
    private bool _disposed;
    private readonly bool _ownsNuGetClient;

    /// <summary>
    /// Project file loader for loading and saving .csproj files.
    /// </summary>
    protected ProjectFileLoader FileLoader { get; }

    /// <summary>
    /// Build validator for post-refactoring build verification.
    /// </summary>
    protected BuildValidator BuildValidator { get; }

    /// <summary>
    /// NuGet client for package metadata and compatibility checking.
    /// </summary>
    protected NuGetClientWrapper NuGetClient { get; }

    /// <summary>
    /// Backup manager for file backup and rollback operations.
    /// </summary>
    protected ProjectFileBackup BackupManager { get; }

    protected ProjectRefactoringBase(
        ILogger? logger = null,
        NuGetClientWrapper? nugetClient = null)
    {
        Logger = logger ?? NullLogger.Instance;
        FileLoader = new ProjectFileLoader(logger as ILogger<ProjectFileLoader>);
        BuildValidator = new BuildValidator(logger as ILogger<BuildValidator>);

        // Track ownership for disposal
        _ownsNuGetClient = nugetClient == null;
        NuGetClient = nugetClient ?? new NuGetClientWrapper(logger as ILogger<NuGetClientWrapper>);

        BackupManager = new ProjectFileBackup(logger as ILogger<ProjectFileBackup>);
    }

    /// <summary>
    /// Validates that a project file exists and is accessible.
    /// </summary>
    /// <param name="projectPath">Path to the project file.</param>
    /// <returns>RefactoringResult indicating success or failure.</returns>
    protected RefactoringResult ValidateProjectFile(string projectPath)
    {
        CurrentPhase = "Project File Validation";

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return RefactoringResult.Failure("Project path cannot be null or empty");
        }

        if (!File.Exists(projectPath))
        {
            return RefactoringResult.Failure($"Project file not found: {projectPath}");
        }

        if (!projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return RefactoringResult.Failure($"Not a valid C# project file: {projectPath}");
        }

        return RefactoringResult.Success(string.Empty, "Project file validation succeeded");
    }

    /// <summary>
    /// Creates a backup of a project file.
    /// Returns the backup path.
    /// </summary>
    /// <param name="filePath">Path to the file to backup.</param>
    /// <returns>Path to the backup file.</returns>
    protected string CreateBackup(string filePath)
    {
        CurrentPhase = "Backup Creation";

        try
        {
            var backupPath = BackupManager.CreateBackup(filePath);
            Logger?.LogInformation("Created backup: {BackupPath}", backupPath);
            return backupPath;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to create backup for {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Creates backups for multiple files.
    /// Returns a dictionary mapping original paths to backup paths.
    /// </summary>
    /// <param name="filePaths">Paths to files to backup.</param>
    /// <returns>Dictionary of original path to backup path.</returns>
    protected Dictionary<string, string> CreateBackups(IEnumerable<string> filePaths)
    {
        CurrentPhase = "Batch Backup Creation";

        try
        {
            var backups = BackupManager.CreateBackups(filePaths);
            Logger?.LogInformation("Created {Count} backups", backups.Count);
            return backups;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to create backups");
            throw;
        }
    }

    /// <summary>
    /// Restores a file from its backup.
    /// </summary>
    /// <param name="filePath">Path to the original file.</param>
    protected void Rollback(string filePath)
    {
        CurrentPhase = "Rollback";

        try
        {
            BackupManager.Restore(filePath);
            Logger?.LogWarning("Rolled back changes to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to rollback {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Restores multiple files from their backups.
    /// </summary>
    /// <param name="filePaths">Paths to files to restore.</param>
    protected void RollbackAll(IEnumerable<string> filePaths)
    {
        CurrentPhase = "Batch Rollback";

        try
        {
            BackupManager.RestoreAll(filePaths);
            Logger?.LogWarning("Rolled back changes to {Count} files", filePaths.Count());
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to rollback files");
            throw;
        }
    }

    /// <summary>
    /// Validates the build after applying changes, with automatic rollback on failure.
    /// </summary>
    /// <param name="projectPath">Path to the project file.</param>
    /// <param name="options">Refactoring options.</param>
    /// <returns>Build validation result wrapped in RefactoringResult.</returns>
    protected async Task<RefactoringResult> ValidateBuildWithRollbackAsync(
        string projectPath,
        ProjectRefactoringOptions options)
    {
        CurrentPhase = "Build Validation";

        if (!options.ValidateBuild)
        {
            return RefactoringResult.Success(string.Empty, "Build validation skipped (disabled in options)");
        }

        try
        {
            var buildResult = await BuildValidator.ValidateBuildAsync(projectPath, options.BuildTimeoutSeconds);

            if (buildResult.IsSuccess)
            {
                Logger?.LogInformation("Build validation succeeded for {ProjectPath}", projectPath);
                return RefactoringResult.Success(string.Empty, buildResult.ToString());
            }
            else
            {
                Logger?.LogError("Build validation failed for {ProjectPath}: {Error}", projectPath, buildResult.ErrorMessage);

                // Attempt rollback
                try
                {
                    Rollback(projectPath);
                    return RefactoringResult.Failure(
                        $"Build failed and changes were rolled back: {buildResult.ErrorMessage}\n\n{buildResult.Errors}");
                }
                catch (Exception rollbackEx)
                {
                    Logger?.LogCritical(rollbackEx, "Failed to rollback after build failure");
                    return RefactoringResult.Failure(
                        $"Build failed AND rollback failed: {buildResult.ErrorMessage}\n\nRollback error: {rollbackEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Build validation threw exception for {ProjectPath}", projectPath);
            return RefactoringResult.Failure($"Build validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates builds for multiple projects with automatic rollback on any failure.
    /// </summary>
    /// <param name="projectPaths">Paths to project files.</param>
    /// <param name="options">Refactoring options.</param>
    /// <returns>Batch operation result with per-project build validation status.</returns>
    protected async Task<BatchOperationResult> ValidateBuildsWithRollbackAsync(
        IEnumerable<string> projectPaths,
        ProjectRefactoringOptions options)
    {
        CurrentPhase = "Batch Build Validation";

        var result = new BatchOperationResult();
        var projectList = projectPaths.ToList();

        if (!options.ValidateBuild)
        {
            result.Skipped.AddRange(projectList.Select(p => (p, "Build validation disabled")));
            return result;
        }

        try
        {
            var buildResults = await BuildValidator.ValidateBuildsAsync(projectList, options.BuildTimeoutSeconds);

            foreach (var (projectPath, buildResult) in buildResults)
            {
                if (buildResult.IsSuccess)
                {
                    result.Succeeded.Add(projectPath);
                }
                else
                {
                    result.Failed[projectPath] = buildResult.ErrorMessage ?? "Build failed";
                }
            }

            // If any builds failed, rollback all changes
            if (result.Failed.Any())
            {
                Logger?.LogError(
                    "Batch build validation failed for {FailedCount} of {TotalCount} projects",
                    result.Failed.Count,
                    projectList.Count);

                try
                {
                    RollbackAll(projectList);
                    result.RolledBack = true;
                }
                catch (Exception rollbackEx)
                {
                    Logger?.LogCritical(rollbackEx, "Failed to rollback after batch build failure");
                    result.RollbackFailed = true;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Batch build validation threw exception");
            result.Failed["*"] = $"Batch build validation error: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Cleans up backups after successful operation.
    /// </summary>
    /// <param name="keepBackups">If true, keeps backup files on disk.</param>
    protected void CleanupBackups(bool keepBackups = false)
    {
        CurrentPhase = "Backup Cleanup";

        try
        {
            BackupManager.DeleteAllBackups(keepBackups);
            Logger?.LogDebug("Cleaned up backups (keepBackups={KeepBackups})", keepBackups);
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to cleanup backups");
        }
    }

    /// <summary>
    /// Disposes resources used by the ProjectRefactoringBase.
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
                // Only dispose NuGetClient if we created it
                if (_ownsNuGetClient)
                {
                    NuGetClient?.Dispose();
                }

                Logger?.LogDebug("ProjectRefactoringBase disposed");
            }

            _disposed = true;
        }
    }
}

/// <summary>
/// Result of a batch operation on multiple project files.
/// </summary>
public class BatchOperationResult
{
    /// <summary>
    /// Projects that were successfully modified.
    /// </summary>
    public List<string> Succeeded { get; } = new();

    /// <summary>
    /// Projects that failed with error messages.
    /// </summary>
    public Dictionary<string, string> Failed { get; } = new();

    /// <summary>
    /// Projects that were skipped with reasons.
    /// </summary>
    public List<(string Path, string Reason)> Skipped { get; } = new();

    /// <summary>
    /// Whether changes were rolled back due to failures.
    /// </summary>
    public bool RolledBack { get; set; }

    /// <summary>
    /// Whether rollback failed (critical situation).
    /// </summary>
    public bool RollbackFailed { get; set; }

    /// <summary>
    /// Total execution time for the batch operation.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Whether the overall batch operation succeeded.
    /// </summary>
    public bool IsSuccess => !Failed.Any() && !RollbackFailed;

    /// <summary>
    /// Summary message for the batch operation.
    /// </summary>
    public string Summary
    {
        get
        {
            var parts = new List<string>();

            if (Succeeded.Any())
            {
                parts.Add($"{Succeeded.Count} succeeded");
            }

            if (Failed.Any())
            {
                parts.Add($"{Failed.Count} failed");
            }

            if (Skipped.Any())
            {
                parts.Add($"{Skipped.Count} skipped");
            }

            if (RolledBack)
            {
                parts.Add("changes rolled back");
            }

            if (RollbackFailed)
            {
                parts.Add("ROLLBACK FAILED");
            }

            return string.Join(", ", parts);
        }
    }
}
