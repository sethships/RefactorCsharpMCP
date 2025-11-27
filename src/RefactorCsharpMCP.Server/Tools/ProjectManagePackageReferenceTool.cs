using RefactorCsharpMCP.Core.ProjectFiles.Models;
using RefactorCsharpMCP.Core.ProjectFiles.Refactorings;
using RefactorCsharpMCP.Core.Validation;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Server.Formatting;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for managing NuGet package references in .csproj files.
/// Supports add, update, and remove operations with framework compatibility validation.
/// </summary>
[McpServerToolType]
public class ProjectManagePackageReferenceTool
{
    private readonly IResponseFormatter _formatter;

    /// <summary>
    /// Creates a new ProjectManagePackageReferenceTool with the specified response formatter.
    /// </summary>
    public ProjectManagePackageReferenceTool(IResponseFormatter formatter)
    {
        _formatter = formatter;
    }

    /// <summary>
    /// Manages a NuGet package reference in a project or solution.
    /// </summary>
    /// <param name="projectPath">Absolute path to .csproj file or solution directory.</param>
    /// <param name="operation">Operation to perform: 'add', 'update', or 'remove'.</param>
    /// <param name="packageId">NuGet package identifier (e.g., 'Newtonsoft.Json').</param>
    /// <param name="version">Package version for add/update operations (e.g., '13.0.3').</param>
    /// <param name="applyToAllProjects">Apply operation to all projects in solution (default: false).</param>
    /// <param name="targetFramework">Target framework for compatibility validation (e.g., 'net8.0').</param>
    /// <param name="dryRun">Preview changes without modifying files (default: false).</param>
    /// <param name="validateBuild">Validate build after operation with auto-rollback (default: true).</param>
    /// <returns>A JSON object containing the operation result and status.</returns>
    [McpServerTool]
    [Description("Add, update, or remove NuGet package references in .NET projects. Supports batch operations, framework compatibility validation, and automatic rollback on build failure.")]
    public async Task<object> ProjectManagePackageReference(
        [Description("Absolute path to .csproj file or solution directory")] string projectPath,
        [Description("Operation: 'add', 'update', or 'remove'")] string operation,
        [Description("NuGet package identifier (e.g., 'Newtonsoft.Json')")] string packageId,
        [Description("Package version for add/update (e.g., '13.0.3')")] string? version = null,
        [Description("Apply to all projects in solution (default: false)")] bool applyToAllProjects = false,
        [Description("Target framework for validation (e.g., 'net8.0')")] string? targetFramework = null,
        [Description("Preview changes without modifying (default: false)")] bool dryRun = false,
        [Description("Validate build after operation (default: true)")] bool validateBuild = true)
    {
        // Input validation
        var validation = ToolInputValidator.ValidateNonEmpty(projectPath, "project path", "Package Management")
                         ?? ToolInputValidator.ValidateNonEmpty(packageId, "package ID", "Package Management");

        if (validation != null)
        {
            return _formatter.Format(validation);
        }

        // Validate project path exists
        if (!File.Exists(projectPath) && !Directory.Exists(projectPath))
        {
            return _formatter.Format(new
            {
                success = false,
                error = $"Project path not found: {projectPath}",
                message = $"Package Management failed: Project path not found"
            });
        }

        // Parse operation
        PackageOperation packageOperation;
        try
        {
            packageOperation = operation.ToLowerInvariant() switch
            {
                "add" => PackageOperation.Add,
                "update" => PackageOperation.Update,
                "remove" => PackageOperation.Remove,
                _ => throw new ArgumentException($"Invalid operation: {operation}. Must be 'add', 'update', or 'remove'")
            };
        }
        catch (ArgumentException ex)
        {
            return _formatter.Format(new
            {
                success = false,
                error = ex.Message,
                message = "Package Management failed: Invalid operation"
            });
        }

        // Validate version is provided for add/update operations
        if ((packageOperation == PackageOperation.Add || packageOperation == PackageOperation.Update)
            && string.IsNullOrWhiteSpace(version))
        {
            return _formatter.Format(new
            {
                success = false,
                error = $"Version is required for {operation} operation",
                message = $"Package Management failed: Version required for {operation}"
            });
        }

        // Create options
        var options = new ProjectRefactoringOptions
        {
            DryRun = dryRun,
            ValidateBuild = validateBuild,
            ApplyToAllProjects = applyToAllProjects,
            PreserveFormatting = true,
            CreateBackup = true
        };

        // Execute the refactoring
        var manager = new PackageReferenceManager();

        try
        {
            var result = await manager.ManagePackageReferenceAsync(
                projectPath,
                packageOperation,
                packageId,
                version,
                options,
                targetFramework,
                CancellationToken.None);

            // Return result
            if (result.IsSuccess)
            {
                return _formatter.Format(new
                {
                    success = true,
                    message = result.Message,
                    operation = operation,
                    packageId = packageId,
                    version = version,
                    dryRun = dryRun,
                    buildValidated = validateBuild && !dryRun
                });
            }
            else
            {
                return _formatter.Format(new
                {
                    success = false,
                    error = result.Message,
                    message = $"Package Management failed: {result.Message}",
                    operation = operation,
                    packageId = packageId
                });
            }
        }
        catch (Exception ex)
        {
            return _formatter.Format(new
            {
                success = false,
                error = ex.Message,
                message = $"Package Management failed with exception: {ex.Message}",
                operation = operation,
                packageId = packageId
            });
        }
    }
}
