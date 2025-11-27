using RefactorCsharpMCP.Core.ProjectFiles.Models;
using RefactorCsharpMCP.Core.ProjectFiles.Refactorings;
using RefactorCsharpMCP.Core.Validation;
using System.ComponentModel;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Server.Formatting;

namespace RefactorCsharpMCP.Server.Tools;

/// <summary>
/// MCP tool for converting legacy .NET Framework projects to SDK-style format.
/// Handles project type detection, framework mapping, and packages.config migration.
/// </summary>
[McpServerToolType]
public class ProjectConvertToSdkStyleTool
{
    private readonly IResponseFormatter _formatter;

    /// <summary>
    /// Creates a new ProjectConvertToSdkStyleTool with the specified response formatter.
    /// </summary>
    public ProjectConvertToSdkStyleTool(IResponseFormatter formatter)
    {
        _formatter = formatter;
    }

    /// <summary>
    /// Converts a legacy .NET Framework project to SDK-style format.
    /// </summary>
    /// <param name="projectPath">Absolute path to the legacy .csproj file.</param>
    /// <param name="allowWebApps">Allow conversion of ASP.NET Web Applications (requires manual migration to ASP.NET Core).</param>
    /// <param name="dryRun">Preview changes without modifying files (default: false).</param>
    /// <param name="validateBuild">Validate build after conversion with auto-rollback (default: true).</param>
    /// <returns>A JSON object containing the conversion result and status.</returns>
    [McpServerTool]
    [Description("Convert legacy .NET Framework projects to SDK-style format. Automatically detects project type, maps frameworks, and migrates packages.config. WARNING: ASP.NET Web Apps require manual migration to ASP.NET Core.")]
    public async Task<object> ProjectConvertToSdkStyle(
        [Description("Absolute path to the legacy .csproj file")] string projectPath,
        [Description("Allow ASP.NET Web App conversion (requires manual ASP.NET Core migration)")] bool allowWebApps = false,
        [Description("Preview changes without modifying (default: false)")] bool dryRun = false,
        [Description("Validate build after conversion (default: true)")] bool validateBuild = true)
    {
        // Input validation
        var validation = ToolInputValidator.ValidateNonEmpty(projectPath, "project path", "SDK Conversion");

        if (validation != null)
        {
            return _formatter.Format(validation);
        }

        // Validate project file exists
        if (!File.Exists(projectPath))
        {
            return _formatter.Format(new
            {
                success = false,
                error = $"Project file not found: {projectPath}",
                message = "SDK Conversion failed: Project file not found"
            });
        }

        // Validate it's a .csproj file
        if (!projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return _formatter.Format(new
            {
                success = false,
                error = "Not a C# project file (must end with .csproj)",
                message = "SDK Conversion failed: Invalid file type"
            });
        }

        // Create options
        var options = new ProjectRefactoringOptions
        {
            DryRun = dryRun,
            ValidateBuild = validateBuild,
            PreserveFormatting = true,
            CreateBackup = true
        };

        // Execute the conversion
        var converter = new SdkStyleConverter();

        try
        {
            var result = await converter.ConvertToSdkStyleAsync(
                projectPath,
                options,
                allowWebApps,
                CancellationToken.None);

            // Return result
            if (result.IsSuccess)
            {
                // Check if this was an ASP.NET Web App conversion
                var isWebAppWarning = result.Message.Contains("ASP.NET Web Application");

                return _formatter.Format(new
                {
                    success = true,
                    message = result.Message,
                    projectPath = projectPath,
                    dryRun = dryRun,
                    buildValidated = validateBuild && !dryRun,
                    warning = isWebAppWarning ? "ASP.NET Web Application converted - manual ASP.NET Core migration recommended" : null
                });
            }
            else
            {
                return _formatter.Format(new
                {
                    success = false,
                    error = result.Message,
                    message = $"SDK Conversion failed: {result.Message}",
                    projectPath = projectPath
                });
            }
        }
        catch (Exception ex)
        {
            return _formatter.Format(new
            {
                success = false,
                error = ex.Message,
                message = $"SDK Conversion failed with exception: {ex.Message}",
                projectPath = projectPath
            });
        }
    }
}
