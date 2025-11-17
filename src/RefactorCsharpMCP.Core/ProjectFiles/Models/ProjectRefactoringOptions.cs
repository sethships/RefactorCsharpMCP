namespace RefactorCsharpMCP.Core.ProjectFiles.Models;

/// <summary>
/// Options for configuring project file refactoring operations.
/// </summary>
public class ProjectRefactoringOptions
{
    /// <summary>
    /// Whether to preview changes without modifying files (dry-run mode).
    /// Default: false (apply changes immediately).
    /// </summary>
    public bool DryRun { get; init; } = false;

    /// <summary>
    /// Whether to validate the build after applying changes.
    /// If true and build fails, changes will be rolled back.
    /// Default: true (safety over speed).
    /// </summary>
    public bool ValidateBuild { get; init; } = true;

    /// <summary>
    /// Whether to create backup files before modification.
    /// Backup files are always created internally for rollback purposes,
    /// this option controls whether they are kept after successful operations.
    /// Default: true (keep backups).
    /// </summary>
    public bool CreateBackup { get; init; } = true;

    /// <summary>
    /// Whether to preserve XML formatting and whitespace.
    /// Default: true (maintain original formatting).
    /// </summary>
    public bool PreserveFormatting { get; init; } = true;

    /// <summary>
    /// Timeout in seconds for build validation operations.
    /// Default: 300 seconds (5 minutes).
    /// </summary>
    public int BuildTimeoutSeconds { get; init; } = 300;

    /// <summary>
    /// Whether to apply operations to all projects in a solution.
    /// Only applicable for solution-level operations.
    /// Default: false (single project only).
    /// </summary>
    public bool ApplyToAllProjects { get; init; } = false;

    /// <summary>
    /// Creates default options with standard settings.
    /// </summary>
    public static ProjectRefactoringOptions Default => new();

    /// <summary>
    /// Creates options for dry-run mode (preview only).
    /// </summary>
    public static ProjectRefactoringOptions DryRunMode => new() { DryRun = true };

    /// <summary>
    /// Creates options for fast execution (no build validation).
    /// Use with caution - only when you're confident the changes are safe.
    /// </summary>
    public static ProjectRefactoringOptions FastMode => new() { ValidateBuild = false };

    /// <summary>
    /// Creates options for batch operations across all projects.
    /// </summary>
    public static ProjectRefactoringOptions BatchMode => new() { ApplyToAllProjects = true };
}

/// <summary>
/// Operation types for package reference management.
/// </summary>
public enum PackageOperation
{
    /// <summary>
    /// Add a new package reference.
    /// </summary>
    Add,

    /// <summary>
    /// Update an existing package reference to a new version.
    /// </summary>
    Update,

    /// <summary>
    /// Remove an existing package reference.
    /// </summary>
    Remove
}

/// <summary>
/// Strategies for resolving version conflicts when enabling Central Package Management.
/// </summary>
public enum ConflictResolutionStrategy
{
    /// <summary>
    /// Fail the operation if any version conflicts are detected.
    /// Requires manual resolution.
    /// </summary>
    Fail,

    /// <summary>
    /// Use the highest version number for conflicting packages.
    /// </summary>
    UseHighest,

    /// <summary>
    /// Use the lowest version number for conflicting packages.
    /// </summary>
    UseLowest,

    /// <summary>
    /// Use the most commonly occurring version across projects.
    /// </summary>
    UseMostCommon
}
