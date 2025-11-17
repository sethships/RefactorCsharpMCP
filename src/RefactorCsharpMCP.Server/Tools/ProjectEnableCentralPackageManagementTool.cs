using RefactorCsharpMCP.Core.ProjectFiles.Models;
using RefactorCsharpMCP.Core.ProjectFiles.Refactorings;
using RefactorCsharpMCP.Core.Validation;
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for enabling Central Package Management (CPM) in .NET solutions.
/// Creates Directory.Build.props and Directory.Packages.props, updates all projects.
/// </summary>
[McpServerToolType]
public class ProjectEnableCentralPackageManagementTool
{
    /// <summary>
    /// Enables Central Package Management for a solution.
    /// </summary>
    /// <param name="solutionPath">Path to solution directory or .sln file.</param>
    /// <param name="conflictStrategy">Strategy for resolving version conflicts: 'fail', 'highest', 'lowest', or 'most_common' (default: 'fail').</param>
    /// <param name="dryRun">Preview changes without modifying files (default: false).</param>
    /// <param name="validateBuild">Validate builds after enabling CPM with auto-rollback (default: true).</param>
    /// <returns>A JSON object containing the CPM enablement result and status.</returns>
    [McpServerTool]
    [Description("Enable Central Package Management (CPM) for a .NET solution. Extracts package versions from all projects, resolves conflicts, creates Directory.Build.props and Directory.Packages.props, and updates all project files. Provides significant maintainability improvements for multi-project solutions.")]
    public async Task<object> ProjectEnableCentralPackageManagement(
        [Description("Path to solution directory or .sln file")] string solutionPath,
        [Description("Conflict resolution strategy: 'fail', 'highest', 'lowest', 'most_common' (default: 'fail')")] string conflictStrategy = "fail",
        [Description("Preview changes without modifying (default: false)")] bool dryRun = false,
        [Description("Validate builds after enabling CPM (default: true)")] bool validateBuild = true)
    {
        // Input validation
        var validation = ToolInputValidator.ValidateNonEmpty(solutionPath, "solution path", "Central Package Management");

        if (validation != null)
        {
            return validation;
        }

        // Validate solution path exists
        if (!Directory.Exists(solutionPath) && !File.Exists(solutionPath))
        {
            return new
            {
                success = false,
                error = $"Solution path not found: {solutionPath}",
                message = "Central Package Management failed: Solution path not found"
            };
        }

        // Parse conflict strategy
        ConflictResolutionStrategy strategy;
        try
        {
            strategy = conflictStrategy.ToLowerInvariant() switch
            {
                "fail" => ConflictResolutionStrategy.Fail,
                "highest" => ConflictResolutionStrategy.UseHighest,
                "lowest" => ConflictResolutionStrategy.UseLowest,
                "most_common" or "mostcommon" => ConflictResolutionStrategy.UseMostCommon,
                _ => throw new ArgumentException(
                    $"Invalid conflict strategy: {conflictStrategy}. Must be 'fail', 'highest', 'lowest', or 'most_common'")
            };
        }
        catch (ArgumentException ex)
        {
            return new
            {
                success = false,
                error = ex.Message,
                message = "Central Package Management failed: Invalid conflict strategy"
            };
        }

        // Create options
        var options = new ProjectRefactoringOptions
        {
            DryRun = dryRun,
            ValidateBuild = validateBuild,
            PreserveFormatting = true,
            CreateBackup = true
        };

        // Execute CPM enablement
        var cpm = new CentralPackageManagement();

        try
        {
            var result = await cpm.EnableCpmAsync(
                solutionPath,
                strategy,
                options,
                CancellationToken.None);

            // Return result
            if (result.IsSuccess)
            {
                // Check if conflicts were resolved
                var hasConflicts = result.Message.Contains("Resolved") && result.Message.Contains("conflicts");

                return new
                {
                    success = true,
                    message = result.Message,
                    solutionPath = solutionPath,
                    conflictStrategy = conflictStrategy,
                    dryRun = dryRun,
                    buildValidated = validateBuild && !dryRun,
                    filesCreated = new[]
                    {
                        "Directory.Build.props",
                        "Directory.Packages.props"
                    },
                    warning = hasConflicts ? $"Version conflicts resolved using '{conflictStrategy}' strategy" : null
                };
            }
            else
            {
                return new
                {
                    success = false,
                    error = result.Message,
                    message = $"Central Package Management failed: {result.Message}",
                    solutionPath = solutionPath
                };
            }
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                error = ex.Message,
                message = $"Central Package Management failed with exception: {ex.Message}",
                solutionPath = solutionPath
            };
        }
    }
}
