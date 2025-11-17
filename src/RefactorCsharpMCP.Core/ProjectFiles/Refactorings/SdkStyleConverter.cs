using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using RefactorCsharpMCP.Core.ProjectFiles.Infrastructure;
using RefactorCsharpMCP.Core.ProjectFiles.Models;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Core.ProjectFiles.Refactorings;

/// <summary>
/// Converts legacy .NET Framework project files to SDK-style format.
/// Handles project type detection, framework mapping, and packages.config migration.
/// </summary>
public class SdkStyleConverter : ProjectRefactoringBase
{
    public SdkStyleConverter(ILogger? logger = null)
        : base(logger)
    {
    }

    /// <summary>
    /// Converts a legacy project file to SDK-style format.
    /// </summary>
    /// <param name="projectPath">Path to the legacy .csproj file.</param>
    /// <param name="options">Refactoring options.</param>
    /// <param name="allowWebApps">Whether to allow ASP.NET Web App conversion (default: false).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Refactoring result with success/failure status.</returns>
    public async Task<RefactoringResult> ConvertToSdkStyleAsync(
        string projectPath,
        ProjectRefactoringOptions? options = null,
        bool allowWebApps = false,
        CancellationToken cancellationToken = default)
    {
        options ??= ProjectRefactoringOptions.Default;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate project file
            CurrentPhase = "Project File Validation";
            var validationResult = ValidateProjectFile(projectPath);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

            // Load project context
            CurrentPhase = "Loading Project";
            var context = FileLoader.LoadProjectContext(projectPath, options.PreserveFormatting);

            // Check if already SDK-style
            if (context.ProjectType == ProjectType.SdkStyle)
            {
                return RefactoringResult.Failure("Project is already in SDK-style format");
            }

            // Check for ASP.NET Web Apps
            if (context.ProjectType == ProjectType.AspNetWebApp && !allowWebApps)
            {
                return RefactoringResult.Failure(
                    "ASP.NET Web Application detected. These require manual migration to ASP.NET Core. " +
                    "Set allowWebApps=true to force conversion, but manual adjustments will be required.");
            }

            // For dry-run mode, show preview
            if (options.DryRun)
            {
                return PreviewConversion(context);
            }

            // Create backup
            CurrentPhase = "Backup Creation";
            var backupPath = CreateBackup(projectPath);

            try
            {
                // Extract metadata from legacy project
                CurrentPhase = "Metadata Extraction";
                var metadata = ExtractProjectMetadata(context);

                // Create SDK-style project XML
                CurrentPhase = "SDK Project Generation";
                var sdkDocument = CreateSdkStyleProject(metadata, context);

                // Migrate packages.config if present
                CurrentPhase = "Package Migration";
                await MigratePackagesConfigAsync(projectPath, sdkDocument, cancellationToken);

                // Save the new project file
                CurrentPhase = "Saving Project";
                FileLoader.SaveProject(sdkDocument, projectPath, options.PreserveFormatting);

                Logger?.LogInformation("Converted project to SDK-style: {ProjectPath}", projectPath);

                // Validate build
                var buildResult = await ValidateBuildWithRollbackAsync(projectPath, options);
                if (!buildResult.IsSuccess)
                {
                    return buildResult;
                }

                // Cleanup backup
                CleanupBackups(options.CreateBackup);

                var warningMessage = metadata.IsAspNetWebApp
                    ? "\n\n⚠️ WARNING: ASP.NET Web Application converted. Manual migration to ASP.NET Core is recommended."
                    : "";

                return RefactoringResult.Success(
                    string.Empty,
                    $"Successfully converted to SDK-style format{warningMessage}");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "SDK conversion failed for {ProjectPath}", projectPath);
                Rollback(projectPath);
                return RefactoringResult.Failure($"Conversion failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger?.LogError(ex, "SDK conversion failed with exception");
            return RefactoringResult.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Previews the conversion in dry-run mode.
    /// </summary>
    private RefactoringResult PreviewConversion(ProjectFileContext context)
    {
        var preview = new StringBuilder();
        preview.AppendLine("DRY RUN - SDK Conversion Preview:");
        preview.AppendLine();
        preview.AppendLine($"Project: {context.ProjectName}");
        preview.AppendLine($"Current Type: {context.ProjectType}");
        preview.AppendLine($"Target Frameworks: {string.Join(", ", context.TargetFrameworks)}");
        preview.AppendLine($"Package References: {context.PackageReferences.Count}");
        preview.AppendLine();
        preview.AppendLine("Changes:");
        preview.AppendLine("- Convert to SDK-style format");
        preview.AppendLine("- Remove explicit file includes (using implicit includes)");
        preview.AppendLine("- Simplify project structure");

        if (context.ProjectType == ProjectType.AspNetWebApp)
        {
            preview.AppendLine();
            preview.AppendLine("⚠️ WARNING: ASP.NET Web Application detected");
            preview.AppendLine("Manual migration to ASP.NET Core recommended");
        }

        return RefactoringResult.Success(string.Empty, preview.ToString());
    }

    /// <summary>
    /// Extracts metadata from a legacy project file.
    /// </summary>
    private ProjectMetadata ExtractProjectMetadata(ProjectFileContext context)
    {
        var document = context.Document!;
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;

        var metadata = new ProjectMetadata
        {
            ProjectName = context.ProjectName,
            AssemblyName = context.AssemblyName ?? context.ProjectName,
            RootNamespace = context.RootNamespace ?? context.ProjectName,
            TargetFrameworks = context.TargetFrameworks,
            PackageReferences = context.PackageReferences,
            IsAspNetWebApp = context.ProjectType == ProjectType.AspNetWebApp
        };

        // Extract OutputType
        var outputType = document.Descendants(ns + ProjectFileConstants.Elements.OutputType)
            .FirstOrDefault()?.Value;

        metadata.OutputType = outputType switch
        {
            "WinExe" => ProjectFileConstants.OutputTypes.WinExe,
            "Exe" => ProjectFileConstants.OutputTypes.Exe,
            "Library" => ProjectFileConstants.OutputTypes.Library,
            _ => ProjectFileConstants.OutputTypes.Library
        };

        // Detect SDK type
        metadata.Sdk = DetermineSdk(document, metadata);

        // Extract nullable and lang version if present
        metadata.Nullable = document.Descendants(ns + ProjectFileConstants.Elements.Nullable)
            .FirstOrDefault()?.Value;
        metadata.LangVersion = document.Descendants(ns + ProjectFileConstants.Elements.LangVersion)
            .FirstOrDefault()?.Value;

        // Check for packages.config
        var projectDir = Path.GetDirectoryName(context.FilePath) ?? string.Empty;
        metadata.HasPackagesConfig = File.Exists(Path.Combine(projectDir, "packages.config"));

        return metadata;
    }

    /// <summary>
    /// Determines the appropriate SDK for the project.
    /// </summary>
    private string DetermineSdk(XDocument document, ProjectMetadata metadata)
    {
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;

        // Check for ASP.NET indicators
        if (metadata.IsAspNetWebApp)
        {
            return ProjectFileConstants.Sdks.MicrosoftNetSdkWeb;
        }

        // Check for WPF/WinForms
        var useWpf = document.Descendants(ns + "UseWPF").FirstOrDefault()?.Value;
        var useWindowsForms = document.Descendants(ns + "UseWindowsForms").FirstOrDefault()?.Value;

        if (useWpf == "true" || useWindowsForms == "true")
        {
            return ProjectFileConstants.Sdks.MicrosoftNetSdkWindowsDesktop;
        }

        // Check project type GUIDs
        var projectTypeGuids = document.Descendants(ns + ProjectFileConstants.Elements.ProjectTypeGuids)
            .FirstOrDefault()?.Value;

        if (!string.IsNullOrWhiteSpace(projectTypeGuids))
        {
            if (projectTypeGuids.Contains(ProjectFileConstants.ProjectTypeGuids.WpfApp))
            {
                return ProjectFileConstants.Sdks.MicrosoftNetSdkWindowsDesktop;
            }
        }

        // Default to standard SDK
        return ProjectFileConstants.Sdks.MicrosoftNetSdk;
    }

    /// <summary>
    /// Creates a new SDK-style project XML document.
    /// </summary>
    private XDocument CreateSdkStyleProject(ProjectMetadata metadata, ProjectFileContext context)
    {
        var project = new XElement("Project",
            new XAttribute("Sdk", metadata.Sdk));

        // PropertyGroup for basic properties
        var propertyGroup = new XElement("PropertyGroup");

        // Target framework(s)
        if (metadata.TargetFrameworks.Count == 1)
        {
            propertyGroup.Add(new XElement("TargetFramework", metadata.TargetFrameworks[0]));
        }
        else if (metadata.TargetFrameworks.Count > 1)
        {
            propertyGroup.Add(new XElement("TargetFrameworks",
                string.Join(";", metadata.TargetFrameworks)));
        }

        // Output type (only if not Library, as Library is the default)
        if (metadata.OutputType != ProjectFileConstants.OutputTypes.Library)
        {
            propertyGroup.Add(new XElement("OutputType", metadata.OutputType));
        }

        // Assembly name (only if different from project name)
        if (metadata.AssemblyName != metadata.ProjectName)
        {
            propertyGroup.Add(new XElement("AssemblyName", metadata.AssemblyName));
        }

        // Root namespace (only if different from project name)
        if (metadata.RootNamespace != metadata.ProjectName)
        {
            propertyGroup.Add(new XElement("RootNamespace", metadata.RootNamespace));
        }

        // Nullable (if specified)
        if (!string.IsNullOrWhiteSpace(metadata.Nullable))
        {
            propertyGroup.Add(new XElement("Nullable", metadata.Nullable));
        }

        // LangVersion (if specified)
        if (!string.IsNullOrWhiteSpace(metadata.LangVersion))
        {
            propertyGroup.Add(new XElement("LangVersion", metadata.LangVersion));
        }

        // Windows-specific properties
        if (metadata.Sdk == ProjectFileConstants.Sdks.MicrosoftNetSdkWindowsDesktop)
        {
            var hasWpf = context.Document!.Descendants("UseWPF").Any(e => e.Value == "true");
            var hasWinForms = context.Document!.Descendants("UseWindowsForms").Any(e => e.Value == "true");

            if (hasWpf)
            {
                propertyGroup.Add(new XElement("UseWPF", "true"));
            }

            if (hasWinForms)
            {
                propertyGroup.Add(new XElement("UseWindowsForms", "true"));
            }
        }

        project.Add(propertyGroup);

        // ItemGroup for PackageReferences
        if (metadata.PackageReferences.Any())
        {
            var itemGroup = new XElement("ItemGroup");

            foreach (var packageRef in metadata.PackageReferences)
            {
                var element = new XElement("PackageReference",
                    new XAttribute("Include", packageRef.PackageId));

                if (!string.IsNullOrWhiteSpace(packageRef.Version))
                {
                    element.Add(new XAttribute("Version", packageRef.Version));
                }

                if (!string.IsNullOrWhiteSpace(packageRef.Condition))
                {
                    element.Add(new XAttribute("Condition", packageRef.Condition));
                }

                if (!string.IsNullOrWhiteSpace(packageRef.PrivateAssets))
                {
                    element.Add(new XElement("PrivateAssets", packageRef.PrivateAssets));
                }

                if (!string.IsNullOrWhiteSpace(packageRef.IncludeAssets))
                {
                    element.Add(new XElement("IncludeAssets", packageRef.IncludeAssets));
                }

                if (!string.IsNullOrWhiteSpace(packageRef.ExcludeAssets))
                {
                    element.Add(new XElement("ExcludeAssets", packageRef.ExcludeAssets));
                }

                itemGroup.Add(element);
            }

            project.Add(itemGroup);
        }

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            project);
    }

    /// <summary>
    /// Migrates packages.config to PackageReference format.
    /// </summary>
    private async Task MigratePackagesConfigAsync(
        string projectPath,
        XDocument sdkDocument,
        CancellationToken cancellationToken)
    {
        var projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;
        var packagesConfigPath = Path.Combine(projectDir, "packages.config");

        if (!File.Exists(packagesConfigPath))
        {
            return;
        }

        try
        {
            Logger?.LogInformation("Migrating packages.config to PackageReference format");

            var packagesConfig = XDocument.Load(packagesConfigPath);
            var packages = packagesConfig.Descendants("package");

            var itemGroup = sdkDocument.Root?.Element("ItemGroup");
            if (itemGroup == null)
            {
                itemGroup = new XElement("ItemGroup");
                sdkDocument.Root?.Add(itemGroup);
            }

            var migratedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var skippedPackages = new List<string>();
            var packageList = packages.ToList();

            foreach (var package in packageList)
            {
                var id = package.Attribute("id")?.Value;
                var version = package.Attribute("version")?.Value;

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
                {
                    Logger?.LogWarning("Skipping invalid package entry in packages.config (missing id or version)");
                    skippedPackages.Add(id ?? "<unknown>");
                    continue;
                }

                // Check if package already exists (might have been in References)
                var exists = itemGroup.Elements("PackageReference")
                    .Any(p => p.Attribute("Include")?.Value.Equals(id, StringComparison.OrdinalIgnoreCase) == true);

                if (!exists)
                {
                    var packageRef = new XElement("PackageReference",
                        new XAttribute("Include", id),
                        new XAttribute("Version", version));

                    itemGroup.Add(packageRef);
                    migratedPackages.Add(id);
                }
                else
                {
                    // Package already exists, count as migrated
                    migratedPackages.Add(id);
                }
            }

            // Only delete packages.config if ALL packages were successfully migrated
            if (skippedPackages.Count == 0 && migratedPackages.Count == packageList.Count)
            {
                File.Delete(packagesConfigPath);
                Logger?.LogInformation(
                    "Deleted packages.config after successful migration of {Count} packages",
                    migratedPackages.Count);
            }
            else
            {
                Logger?.LogWarning(
                    "Kept packages.config due to partial migration: {Migrated}/{Total} packages migrated, {Skipped} skipped",
                    migratedPackages.Count,
                    packageList.Count,
                    skippedPackages.Count);
            }
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to migrate packages.config, keeping original file");
        }
    }
}

/// <summary>
/// Metadata extracted from a legacy project file.
/// </summary>
internal class ProjectMetadata
{
    public required string ProjectName { get; init; }
    public required string AssemblyName { get; init; }
    public required string RootNamespace { get; init; }
    public required List<string> TargetFrameworks { get; init; }
    public required string OutputType { get; set; }
    public required string Sdk { get; set; }
    public string? Nullable { get; set; }
    public string? LangVersion { get; set; }
    public List<PackageReference> PackageReferences { get; init; } = new();
    public bool HasPackagesConfig { get; set; }
    public bool IsAspNetWebApp { get; set; }
}
